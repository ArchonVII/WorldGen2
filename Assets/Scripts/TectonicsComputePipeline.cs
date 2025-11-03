using UnityEngine;

/// <summary>
/// A helper class that encapsulates the logic for running the
/// tectonics-related compute shaders.
/// </summary>
public class TectonicsComputePipeline
{
	private ComputeShader plateIdShader;
	private ComputeShader primordialNoiseShader;
	private float noiseFrequency;
	private float noiseAmplitude;

	public TectonicsComputePipeline(ComputeShader plateIdShader, ComputeShader primordialNoiseShader, float noiseFrequency, float noiseAmplitude)
	{
		this.plateIdShader = plateIdShader;
		this.primordialNoiseShader = primordialNoiseShader;
		this.noiseFrequency = noiseFrequency;
		this.noiseAmplitude = noiseAmplitude;
	}

	/// <summary>
	/// Runs the full compute pipeline on the given PlanetData.
	/// </summary>
	public void Run(PlanetData data)
	{
		RunPlateIDShader(data);
		RunPrimordialNoiseShader(data);
	}

	void RunPlateIDShader(PlanetData data)
	{
		if (plateIdShader == null || data.TectonicPlatesBuffer == null)
		{
			Debug.LogError("Plate ID Shader or Plate Buffer is missing!");
			return;
		}
        
		int kernel = plateIdShader.FindKernel("CSMain");
        
		plateIdShader.SetBuffer(kernel, "_PlateDataBuffer", data.TectonicPlatesBuffer);
		plateIdShader.SetTexture(kernel, "_PlateIDTexture", data.PlateIDTexture);
		plateIdShader.SetInt("_NumPlates", data.TectonicPlates.Count);
		plateIdShader.SetInts("_Resolution", data.PlateIDTexture.width, data.PlateIDTexture.height);
        
		int threadGroupsX = Mathf.CeilToInt(data.PlateIDTexture.width / 8.0f);
		int threadGroupsY = Mathf.CeilToInt(data.PlateIDTexture.height / 8.0f);
		plateIdShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        
		Debug.Log("Compute Shader: Generated Plate ID Texture.");
	}
    
	void RunPrimordialNoiseShader(PlanetData data)
	{
		if (primordialNoiseShader == null || data.Heightmap == null)
		{
			Debug.LogError("Primordial Noise Shader or Heightmap is missing!");
			return;
		}
        
		int kernel = primordialNoiseShader.FindKernel("CSMain");
        
		primordialNoiseShader.SetTexture(kernel, "_Heightmap", data.Heightmap);
		primordialNoiseShader.SetInts("_Resolution", data.Heightmap.width, data.Heightmap.height);
		primordialNoiseShader.SetFloat("_Frequency", noiseFrequency);
		primordialNoiseShader.SetFloat("_Amplitude", noiseAmplitude);
        
		int threadGroupsX = Mathf.CeilToInt(data.Heightmap.width / 8.0f);
		int threadGroupsY = Mathf.CeilToInt(data.Heightmap.height / 8.0f);
		primordialNoiseShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        
		Debug.Log("Compute Shader: Generated Primordial Noise.");
	}
}
