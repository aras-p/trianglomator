using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

[StructLayout(LayoutKind.Sequential)]
public struct Triangle
{
	public Vector2 posA, posB, posC;
	public Color col;
}

[StructLayout(LayoutKind.Sequential)]
public struct Params
{
	public int triCount;
	public int width;
	public int height;
}

[StructLayout(LayoutKind.Sequential)]
public struct Score
{
	public uint score;
	public uint bestScore;
	public uint iterations;
	public uint improvements;
	public uint rng;
}

public class Trianglomator : MonoBehaviour
{
	public int m_Triangles = 100;
	public int m_IterationsPerUpdate = 10;
	public ComputeShader m_CS;
	public Shader m_Shader;
	public UnityEngine.UI.RawImage m_Source;
	public UnityEngine.UI.RawImage m_DestImage;
	public UnityEngine.UI.RawImage m_BestImage;
	public UnityEngine.UI.Text m_Text;

	RenderTexture m_RT;
	RenderTexture m_RTBest;
	ComputeBuffer m_DNA;
	ComputeBuffer m_Params;
	ComputeBuffer m_Score;
	Material m_Material;
	int m_GotFitness = 92;
	string m_Log = "";

	int m_KernelCopy, m_KernelMutate, m_KernelScore;
	uint m_GroupsizeCopy, m_GroupsizeScoreX, m_GroupsizeScoreY;

	void OnDestroy()
	{
		DestroyImmediate(m_RT);
		DestroyImmediate(m_RTBest);
		DestroyImmediate(m_Material);
		if (m_DNA != null) m_DNA.Dispose();
		m_DNA = null;
		if (m_Params != null) m_Params.Dispose();
		m_Params = null;
		if (m_Score != null) m_Score.Dispose();
		m_Score = null;
	}

	void Update ()
	{
		if (!m_CS)
			return;
		if (!m_Shader)
			return;
		var sourceTex = m_Source.texture;
		if (!sourceTex)
			return;

		if (!m_RT)
		{
			m_KernelCopy = m_CS.FindKernel("CSCopy");
			m_KernelMutate = m_CS.FindKernel("CSMutate");
			m_KernelScore = m_CS.FindKernel("CSCalcScore");
			uint dummyY, dummyZ;
			m_CS.GetKernelThreadGroupSizes(m_KernelCopy, out m_GroupsizeCopy, out dummyY, out dummyZ);
			m_CS.GetKernelThreadGroupSizes(m_KernelScore, out m_GroupsizeScoreX, out m_GroupsizeScoreY, out dummyZ);

			var desc = new RenderTextureDescriptor
			{
				width = sourceTex.width,
				height = sourceTex.height,
				dimension = TextureDimension.Tex2D,
				volumeDepth = 1,
				msaaSamples = 1,
				enableRandomWrite = true
			};
			m_RT = new RenderTexture(desc);
			m_RT.Create();
			m_DestImage.texture = m_RT;
			m_RTBest = new RenderTexture(desc);
			m_RTBest.Create();
			m_BestImage.texture = m_RTBest;
			m_CS.SetTexture(m_KernelScore, "_SourceTex", sourceTex);
			m_CS.SetTexture(m_KernelScore, "_DestTex", m_RT);
			m_Log = $"{sourceTex.name} tris: {m_Triangles}\n";
		}

		if (m_DNA == null)
		{
			m_DNA = new ComputeBuffer(m_Triangles * 2, Marshal.SizeOf(typeof(Triangle)));
			var initData = new Triangle[m_Triangles * 2];
			Random.InitState(1);
			for (var i = 0; i < m_Triangles; ++i)
			{
				initData[i].posA = new Vector2(Random.value, Random.value);
				initData[i].posB = new Vector2(Random.value, Random.value);
				initData[i].posC = new Vector2(Random.value, Random.value);
				initData[i].col = new Color(Random.value, Random.value, Random.value, Random.value);
				initData[i + m_Triangles] = initData[i];
			}
			m_DNA.SetData(initData);
			m_CS.SetBuffer(m_KernelCopy, "_DNA", m_DNA);
			m_CS.SetBuffer(m_KernelMutate, "_DNA", m_DNA);
		}

		if (m_Params == null)
		{
			m_Params = new ComputeBuffer(1, Marshal.SizeOf(typeof(Params)));
			var para = new Params[1];
			para[0].triCount = m_Triangles;
			para[0].width = sourceTex.width;
			para[0].height = sourceTex.height;
			m_Params.SetData(para);

			m_CS.SetBuffer(m_KernelCopy, "_Params", m_Params);
			m_CS.SetBuffer(m_KernelMutate, "_Params", m_Params);
			m_CS.SetBuffer(m_KernelScore, "_Params", m_Params);
		}

		var sc = new Score[1];
		if (m_Score == null)
		{
			m_Score = new ComputeBuffer(1, Marshal.SizeOf(typeof(Score)));
			sc[0].score = 0xFFFFFFFF;
			sc[0].bestScore = 0xFFFFFFFF;
			sc[0].iterations = 0;
			sc[0].improvements = 0;
			sc[0].rng = 1;
			m_Score.SetData(sc);

			m_CS.SetBuffer(m_KernelCopy, "_Score", m_Score);
			m_CS.SetBuffer(m_KernelMutate, "_Score", m_Score);
			m_CS.SetBuffer(m_KernelScore, "_Score", m_Score);
		}

		if (!m_Material)
		{
			m_Material = new Material(m_Shader);
			m_Material.SetBuffer("_DNA", m_DNA);
		}

		for (var it = 0; it < m_IterationsPerUpdate; ++it)
		{
			m_CS.Dispatch(m_KernelCopy, (m_Triangles+(int)m_GroupsizeCopy-1)/(int)m_GroupsizeCopy, 1, 1);
			m_CS.Dispatch(m_KernelMutate, 1, 1, 1);

			Graphics.SetRenderTarget(m_RT);
			GL.Clear(false, true, Color.white);
			m_Material.SetInt("_StartVertex", 0);
			m_Material.SetPass(0);
			Graphics.DrawProcedural(MeshTopology.Triangles, m_Triangles * 3);

			m_CS.Dispatch(m_KernelScore, (sourceTex.width+(int)m_GroupsizeScoreX-1)/(int)m_GroupsizeScoreX, (sourceTex.height+(int)m_GroupsizeScoreY-1)/(int)m_GroupsizeScoreY, 1);
		}

		m_Score.GetData(sc);
		var fitness = (1.0 - sc[0].bestScore / (double) (sourceTex.width * sourceTex.height * 3 * 255)) * 100.0;
		var time = Time.realtimeSinceStartup;
		m_Text.text = $"fit {fitness:F3}% time {time:F2}s\niter {sc[0].iterations} impr {sc[0].improvements}\n{m_Log}";
		var ifitness = (int) fitness;
		if (ifitness > m_GotFitness)
		{
			m_Log += $"got {fitness:F3}% at time {time:F2}, iters {sc[0].iterations}\n";
			m_GotFitness = ifitness;
			Debug.Log(m_Log);
		}

		Graphics.SetRenderTarget(m_RTBest);
		GL.Clear(false, true, Color.white);
		m_Material.SetInt("_StartVertex", m_Triangles * 3);
		m_Material.SetPass(0);
		Graphics.DrawProcedural(MeshTopology.Triangles, m_Triangles * 3);
	}

	void OnGUI()
	{
		if (GUILayout.Button("Dump Triangles"))
		{
			var tris = new Triangle[m_Triangles];
			m_DNA.GetData(tris, 0, m_Triangles, m_Triangles);
			var sb = new StringBuilder();
			foreach (var tri in tris)
			{
				sb.AppendLine($"Tri({tri.posA.x:F3},{tri.posA.y:F3},{tri.posB.x:F3},{tri.posB.y:F3},{tri.posC.x:F3},{tri.posC.y:F3}, vec3({tri.col.r:F3},{tri.col.g:F3},{tri.col.b:F3}), {tri.col.a:F3});");
			}

			File.WriteAllText("output.txt", sb.ToString());
			Debug.Log($"Wrote output.txt at {Time.realtimeSinceStartup}");
		}
	}
}
