using UnityEngine;

/// <summary>
/// A wrapper class to handle dispatching the compute shader pipeline.
/// This keeps the dispatch logic separate from the main PlanetDirector.
/// [cite: GeminiUpload/Planet_Generator_Architecture_Review.md]
/// </summary>
public class TectonicsComputePipeline
{
	private ComputeShader plateIdShader;
	private ComputeShader primordialNoiseShader;
    
	// Noise settings
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
	/// Runs the full compute pipeline in order.
	/// </summary>
	public void Run(PlanetData data)
	{
		RunPlateIDShader(data);
		RunPrimordialNoiseShader(data);
	}

	/// <summary>
	/// Step 1: Generate Plate ID and Boundary Delta textures
	/// </summary>
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
        
		// --- THIS IS THE MISSING LINE ---
		// We must set the new boundary texture just like we set the plate ID texture
		plateIdShader.SetTexture(kernel, "_BoundaryDeltaTexture", data.BoundaryDeltaTexture);
		// --- END FIX ---
        
		plateIdShader.SetInt("_NumPlates", data.TectonicPlates.Count);
		plateIdShader.SetInts("_Resolution", data.PlateIDTexture.width, data.PlateIDTexture.height);
        
		// Dispatch the shader
		int threadGroupsX = Mathf.CeilToInt(data.PlateIDTexture.width / 8.0f);
		int threadGroupsY = Mathf.CeilToInt(data.PlateIDTexture.height / 8.0f);
		plateIdShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        
		Debug.Log("Compute Shader: Generated Plate ID Texture.");
	}
    
	/// <summary>
	/// Step 2: Generate Primordial Noise texture
	/// </summary>
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
        
		// Dispatch the shader
		int threadGroupsX = Mathf.CeilToInt(data.Heightmap.width / 8.0f);
		int threadGroupsY = Mathf.CeilToInt(data.Heightmap.height / 8.0f);
		primordialNoiseShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        
		Debug.Log("Compute Shader: Generated Primordial Noise.");
	}
}

