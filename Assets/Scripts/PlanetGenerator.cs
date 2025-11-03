using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Optimized planet generator.
/// This version is simplified to only generate the data needed for the shader-based overlay.
/// </summary>
public static class PlanetGenerator
{
	/// <summary>
	/// Main entry point for planet generation
	/// </summary>
	public static PlanetData Generate(PlanetGenerationConfig config)
	{
		PlanetData data = new PlanetData(config);

		if (config.randomizeOnStart)
		{
			data.config = RandomizeConfig(config);
		}

		CalculatePlanetProfile(data);

		if (data.HasTectonics)
		{
			GenerateTectonicPlates(data);
			
			// --- NEW ---
			// Create the empty GPU assets that our compute shaders will fill
			CreateComputeAssets(data);
		}

		return data;
	}

	private static PlanetGenerationConfig RandomizeConfig(PlanetGenerationConfig config)
	{
		config.planetZone = (HabitableZone)Random.Range(0, 3);
		config.planetRadius = Random.Range(0.25f, 2.0f);
		config.planetAgeInBillions = Random.Range(0.5f, 10f);
		config.waterAbundance = Random.Range(0f, 1f);
		config.hasLargeMoon = Random.Range(0, 2) > 0;
		config.numTectonicPlates = Random.Range(5, 40);
		config.oceanicPlateChance = Random.Range(0.4f, 0.8f);
		return config;
	}

	private static void CalculatePlanetProfile(PlanetData data)
	{
		var config = data.config;
		
		float engineScore = config.planetRadius - (config.planetAgeInBillions / 10f);
		data.HasActiveGeology = engineScore > 0.5f;
		data.HasLiquidWater = config.planetZone == HabitableZone.Habitable && config.waterAbundance > 0.1f;
		data.HasTectonics = data.HasActiveGeology && data.HasLiquidWater;
		data.HasMagneticField = data.HasActiveGeology;

		if (data.HasTectonics)
		{
			data.GeneratedType = data.HasLiquidWater ? GeneratedPlanetType.EarthLike : GeneratedPlanetType.VolcanicWorld;
			data.PlanetColor = data.HasLiquidWater ? new Color(0.2f, 0.4f, 0.8f) : new Color(0.7f, 0.2f, 0.1f);
		}
		else
		{
			data.GeneratedType = data.HasLiquidWater ? GeneratedPlanetType.IceShellWorld : GeneratedPlanetType.CratereWorld;
			data.PlanetColor = data.HasLiquidWater ? new Color(0.9f, 0.9f, 1.0f) : new Color(0.5f, 0.5f, 0.5f);
		}
	}

	private static void GenerateTectonicPlates(PlanetData data)
	{
		int seed = (int)(data.config.planetAgeInBillions * 1000);
		Random.InitState(seed);

		// Create plates with random seed points
		data.TectonicPlates = new List<TectonicPlate>();
		for (int i = 0; i < data.config.numTectonicPlates; i++)
		{
			// Distribute points more evenly using stratified sampling
			float u = Random.Range(0f, 1f);
			float v = Random.Range(0f, 1f);
			
			// Optional: Apply slight jitter to avoid too-regular patterns
			u += Random.Range(-0.1f, 0.1f) / data.config.numTectonicPlates;
			v += Random.Range(-0.1f, 0.1f) / data.config.numTectonicPlates;
			u = Mathf.Repeat(u, 1f); // Wrap around
			v = Mathf.Clamp01(v);
			
			data.TectonicPlates.Add(new TectonicPlate(i, new Vector2(u, v), data.config));
		}


	}
	/// <summary>
	/// Creates the empty RenderTextures and ComputeBuffer on the GPU.
	/// </summary>
	private static void CreateComputeAssets(PlanetData data)
	{
		int texWidth = data.config.plateMapResolutionX;
		int texHeight = data.config.plateMapResolutionY;

		// Create PlateID texture (8-bit, for 0-255 IDs)
		data.PlateIDTexture = CreateWritableRenderTexture(texWidth, texHeight, RenderTextureFormat.R8);

		
		// Create Heightmap texture (32-bit float, for high-precision 0-1 height)
		data.Heightmap = CreateWritableRenderTexture(texWidth, texHeight, RenderTextureFormat.RFloat);

		// --- Create and fill the ComputeBuffer ---
		if (data.TectonicPlates.Count > 0)
		{
			// 1. Create C# array of GPU-safe structs
			var gpuData = new PlateGpuData[data.TectonicPlates.Count];
			for (int i = 0; i < data.TectonicPlates.Count; i++)
			{
				var plate = data.TectonicPlates[i];
				gpuData[i] = new PlateGpuData
				{
					center3D = plate.Center3D,
					movementVector = plate.MovementVector,
					speed = plate.Speed,
					isOceanic = plate.IsOceanic ? 1 : 0
				};
			}
			
			// 2. Create ComputeBuffer and set its data
			data.TectonicPlatesBuffer = new ComputeBuffer(data.TectonicPlates.Count, PlateGpuData.GetStride());
			data.TectonicPlatesBuffer.SetData(gpuData);
		}
	}

	/// Helper function to create a RenderTexture configured for compute shader output.
	private static RenderTexture CreateWritableRenderTexture(int width, int height, RenderTextureFormat format)
	{
		// --- FIX --- Added 'RenderTextureReadWrite.Linear' to tell Unity this is a data texture,
		// which prevents the sRGB warning and ensures correct data handling.
		var rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear);
		rt.enableRandomWrite = true; // IMPORTANT: Allows compute shader to write to it
		rt.wrapMode = TextureWrapMode.Repeat;
		rt.filterMode = FilterMode.Bilinear;
		rt.Create(); // Allocate the texture on the GPU
		return rt;
	}
	
	
}


