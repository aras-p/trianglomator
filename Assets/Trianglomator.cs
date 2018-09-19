using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public struct Triangle
{
	public Vector2 posA, posB, posC;
	public Color col;
}

public struct Params
{
	public int triCount;
	public int frame;
	public int width;
	public int height;
}

public struct Score
{
	public uint score;
	public uint bestScore;
	public uint iterations;
	public uint improvements;
}

public class Trianglomator : MonoBehaviour
{
	public Texture2D m_SourceTex;
	public int m_Triangles = 100;
	public int m_IterationsPerUpdate = 10;
	public ComputeShader m_CS;
	public Shader m_Shader;
	public UnityEngine.UI.RawImage m_DestImage;
	public UnityEngine.UI.RawImage m_BestImage;
	public UnityEngine.UI.Text m_Text;

	RenderTexture m_RT;
	RenderTexture m_RTBest;
	ComputeBuffer m_DNA;
	ComputeBuffer m_Params;
	ComputeBuffer m_Score;
	Material m_Material;
	int m_Counter;
	int m_GotFitness = 90;
	string m_Log = "";

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
		if (!m_SourceTex)
			return;

		if (!m_RT)
		{
			var desc = new RenderTextureDescriptor
			{
				width = m_SourceTex.width,
				height = m_SourceTex.height,
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
			m_CS.SetTexture(2, "_SourceTex", m_SourceTex);
			m_CS.SetTexture(2, "_DestTex", m_RT);
		}

		if (m_DNA == null)
		{
			m_DNA = new ComputeBuffer(m_Triangles * 2, Marshal.SizeOf(typeof(Triangle)));
			var initData = new Triangle[m_Triangles * 2];
			Random.seed = 1;
			for (var i = 0; i < m_Triangles; ++i)
			{
				initData[i].posA = new Vector2(Random.value, Random.value);
				initData[i].posB = new Vector2(Random.value, Random.value);
				initData[i].posC = new Vector2(Random.value, Random.value);
				initData[i].col = new Color(Random.value, Random.value, Random.value, Random.value);
				initData[i + m_Triangles] = initData[i];
			}
			m_DNA.SetData(initData);
			m_CS.SetBuffer(0, "_DNA", m_DNA);
			m_CS.SetBuffer(1, "_DNA", m_DNA);
		}

		if (m_Params == null)
		{
			m_Params = new ComputeBuffer(1, Marshal.SizeOf(typeof(Params)));
			m_CS.SetBuffer(0, "_Params", m_Params);
			m_CS.SetBuffer(1, "_Params", m_Params);
			m_CS.SetBuffer(2, "_Params", m_Params);
		}

		var sc = new Score[1];
		if (m_Score == null)
		{
			m_Score = new ComputeBuffer(1, Marshal.SizeOf(typeof(Score)));
			m_CS.SetBuffer(0, "_Score", m_Score);
			m_CS.SetBuffer(1, "_Score", m_Score);
			m_CS.SetBuffer(2, "_Score", m_Score);
			sc[0].score = 0xFFFFFFFF;
			sc[0].bestScore = 0xFFFFFFFF;
			sc[0].iterations = 0;
			sc[0].improvements = 0;
			m_Score.SetData(sc);
		}

		if (!m_Material)
		{
			m_Material = new Material(m_Shader);
			m_Material.SetBuffer("_DNA", m_DNA);
		}

		var para = new Params[1];
		for (var it = 0; it < m_IterationsPerUpdate; ++it)
		{
			para[0].triCount = m_Triangles;
			para[0].frame = ++m_Counter;
			para[0].width = m_SourceTex.width;
			para[0].height = m_SourceTex.height;
			m_Params.SetData(para);

			m_CS.Dispatch(0, (m_Triangles+63)/64, 1, 1);
			m_CS.Dispatch(1, 1, 1, 1);

			Graphics.SetRenderTarget(m_RT);
			GL.Clear(false, true, Color.white);
			m_Material.SetInt("_StartVertex", 0);
			m_Material.SetPass(0);
			Graphics.DrawProcedural(MeshTopology.Triangles, m_Triangles * 3);

			m_CS.Dispatch(2, (m_SourceTex.width+7)/8, (m_SourceTex.height+7)/8, 1);
		}

		m_Score.GetData(sc);
		var fitness = (1.0 - sc[0].bestScore / (double) (m_SourceTex.width * m_SourceTex.height * 3 * 255)) * 100.0;
		var time = Time.realtimeSinceStartup;
		m_Text.text = $"fit {fitness:F2}% time {time:F2}s\niter {sc[0].iterations} impr {sc[0].improvements}\n{m_Log}";
		var ifitness = (int) fitness;
		if (ifitness > m_GotFitness)
		{
			m_Log += $"got {fitness:F2}% at time {time:F2}, iters {sc[0].iterations}\n";
			m_GotFitness = ifitness;
		}

		Graphics.SetRenderTarget(m_RTBest);
		GL.Clear(false, true, Color.white);
		m_Material.SetInt("_StartVertex", m_Triangles * 3);
		m_Material.SetPass(0);
		Graphics.DrawProcedural(MeshTopology.Triangles, m_Triangles * 3);
	}
}
