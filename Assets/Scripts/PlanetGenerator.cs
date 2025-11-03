using UnityEngine;
using System.Collections.Generic;
using System.Linq;


public static class PlanetGenerator
{

	/// Main entry point for planet generation
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
			CreateComputeAssets(data);
		}

		return data;
	}

	private static PlanetGenerationConfig RandomizeConfig(PlanetGenerationConfig config)
	{
		
		config.planetSeed = Random.Range(int.MinValue, int.MaxValue);
		config.planetZone = (HabitableZone)Random.Range(0, 3);
		config.planetRadius = Random.Range(0.25f, 2.0f);
		config.planetAgeInBillions = Random.Range(0.5f, 10f);
		config.waterAbundance = Random.Range(0f, 1f);
		config.hasLargeMoon = Random.Range(0, 2) > 0;
		config.numTectonicPlates = Random.Range(5, 40);
		config.oceanicPlateChance = Random.Range(0.4f, 0.8f);
		
		// Randomize velocity fields
		config.velocityModel = (PlateVelocityModel)Random.Range(0, 2); // 0=Random, 1=AxisFlows2
		config.minPlateSpeed = Random.Range(0.2f, 0.8f);
		config.maxPlateSpeed = Random.Range(1.0f, 2.5f);
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
		// --- MODIFIED: Use new planetSeed ---
		int seedToUse = data.config.planetSeed;
		if (seedToUse == 0)
		{
			// Fallback to old logic if seed is 0
			seedToUse = (int)(data.config.planetAgeInBillions * 1000) + 1;
			if (seedToUse == 0) seedToUse = 1; // Handle edge case
		}
		Random.InitState(seedToUse);

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

		// --- NEW: Apply velocity model after plates are created ---
		ApplyPlateVelocityModel(data);
	}

	private static void ApplyPlateVelocityModel(PlanetData data)
	{
		

		// Setup for AxisFlows model
		Vector3 axis1 = Random.insideUnitSphere.normalized;
		Vector3 axis2 = Random.insideUnitSphere.normalized;
		float weight1 = Random.Range(-0.5f, 0.5f);
		float weight2 = Random.Range(-0.5f, 0.5f);

		foreach (var plate in data.TectonicPlates)
		{
			Vector3 p = plate.Center3D; // Plate's 3D center position
			Vector3 v_dir = Vector3.zero; // Final 3D velocity direction
			
			// 1. Calculate base speed
			plate.Speed = Random.Range(data.config.minPlateSpeed, data.config.maxPlateSpeed);

			// 2. Calculate movement direction vector
			switch (data.config.velocityModel)
			{
			case PlateVelocityModel.AxisFlows2:
			case PlateVelocityModel.AxisFlows4: // For now, just use 2 axes
				// v(p) = Σ w_k * (a_k × p)
				// This vector is already tangent to the sphere.
				v_dir = (Vector3.Cross(axis1, p) * weight1) + 
				(Vector3.Cross(axis2, p) * weight2);
				break;
					
			case PlateVelocityModel.Random:
			default:
				// Original logic, but now in 3D
				// Get a random vector and find its tangent to the sphere
				Vector3 randomVec = Random.insideUnitSphere;
				v_dir = Vector3.Cross(p, randomVec); // Cross product gives a tangent
				break;
			}
			
			// 3. Apply the direction (normalized)
			if (v_dir.sqrMagnitude > 0.001f)
			{
				plate.MovementVector = v_dir.normalized;
			}
			else
			{
				// Handle edge case (e.g., if p lines up with axis)
				plate.MovementVector = Vector3.Cross(p, Vector3.up).normalized;
				if (plate.MovementVector.sqrMagnitude < 0.001f)
					plate.MovementVector = Vector3.Cross(p, Vector3.right).normalized;
			}
		}
	}

	

	/// Creates the empty RenderTextures and ComputeBuffer on the GPU.
	private static void CreateComputeAssets(PlanetData data)
	{
		int texWidth = data.config.plateMapResolutionX;
		int texHeight = data.config.plateMapResolutionY;

		// Create PlateID texture (8-bit, for 0-255 IDs)
		data.PlateIDTexture = CreateWritableRenderTexture(texWidth, texHeight, RenderTextureFormat.R8);

		// Create Heightmap texture (32-bit float, for high-precision 0-1 height)
		data.Heightmap = CreateWritableRenderTexture(texWidth, texHeight, RenderTextureFormat.RFloat);

		data.BoundaryDeltaTexture = CreateWritableRenderTexture(texWidth, texHeight, RenderTextureFormat.RHalf);

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

		var rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear);
		rt.enableRandomWrite = true; // IMPORTANT: Allows compute shader to write to it
		rt.wrapMode = TextureWrapMode.Repeat;
		rt.filterMode = FilterMode.Bilinear;
		rt.Create(); // Allocate the texture on the GPU
		return rt;
	}
	
	
}


