using UnityEngine;
using System.Collections.Generic;

// --- Enums (Dropdown Menus) ---

/// <summary>
/// Defines the planet's position relative to the star's habitable zone.
/// This is our high-level "StarDistance" variable.
/// </summary>
public enum HabitableZone
{
	TooHot,
	Habitable,
	TooCold
}

/// <summary>
/// Defines the high-level *output* type of the planet.
/// This is what the generator will *decide* based on your inputs.
/// </summary>
public enum GeneratedPlanetType
{
	EarthLike,        // Has Tectonics, Has Water
	VolcanicWorld,    // Has Tectonics, No Water (Venus-Like / Mustafar)
	CratereWorld,     // No Tectonics, No Water (Mars-Like / Moon)
	IceShellWorld     // No Tectonics, Has Water (Europa-Like)
}

/// <summary>
/// A "struct" to hold all the *input* variables for our generator.
/// We pass this to the generator.
/// </summary>
[System.Serializable]
public struct PlanetGenerationConfig
{
	[Header("Behavior")]
	[Tooltip("If true, all inputs below will be randomized on Start.")]
	public bool randomizeOnStart;

	[Header("Star System (Climate)")]
	[Tooltip("Controls the surface temperature and potential for liquid water.")]
	public HabitableZone planetZone;

	[Header("Planet Internals (Engine)")]
	[Tooltip("Normalized radius (1.0 = 1 Earth Radius). Controls internal heat.")]
	[Range(0.25f, 2.0f)]
	public float planetRadius;

	[Tooltip("Age in Billions of Years. Older planets are cooler and less active.")]
	[Range(0.5f, 10f)]
	public float planetAgeInBillions;

	[Header("Planet Composition (Modifiers)")]
	[Tooltip("0.0 = Bone dry (Mars)\n" +
	"0.5 = Balanced (Earth)\n" +
	"1.0 = Global Ocean (Waterworld)")]
	[Range(0f, 1f)]
	public float waterAbundance;

	[Tooltip("Does this planet have a large, stabilizing moon?")]
	public bool hasLargeMoon;

	[Header("Tectonics")]
	[Tooltip("Number of tectonic plates to generate.")]
	[Range(3, 100)]
	public int numTectonicPlates;

	[Tooltip("The chance (0-1) that a new plate will be oceanic vs continental.")]
	[Range(0f, 1f)]
	public float oceanicPlateChance;

	[Header("Resolutions")]
	[Tooltip("The width of the generated plate ID map. Higher = more precise boundaries.")]
	public int plateMapResolutionX;

	[Tooltip("The height of the generated plate ID map. Higher = more precise boundaries.")]
	public int plateMapResolutionY;
}

// --- THIS STRUCT WAS MISSING ---
/// <summary>
/// This struct MUST match the layout in the compute shaders.
/// It's used to send plate data to the GPU.
/// Note: 'bool' is tricky, so we use 'int isOceanic' (0 or 1).
/// </summary>
public struct PlateGpuData
{
	public Vector3 center3D;
	public Vector2 movementVector;
	public float speed;
	public int isOceanic;

	// We must define the size (stride) for the compute buffer
	public static int GetStride()
	{
		// Vector3 (3) + Vector2 (2) + float (1) + int (1) = 7 floats
		// (int is 4 bytes, same as float)
		return sizeof(float) * (3 + 2 + 1) + sizeof(int);
	}
}


/// <summary>
/// Represents a single tectonic plate with its properties
/// </summary>
public class TectonicPlate
{
	public int ID;
	public Vector2 Center;  // Seed point in UV space (0-1)
	public Vector3 Center3D; // Pre-calculated 3D position on unit sphere
	public Color Color;     // For visualization
	public Vector2 MovementVector;  // Direction and speed of plate movement
	public float Speed;
	public bool IsOceanic;  // Will be used in future heightmap generation
	
	public TectonicPlate(int id, Vector2 center, PlanetGenerationConfig config)
	{
		ID = id;
		Center = center;
		Color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f);
		
		// Pre-calculate 3D center point for fast distance checks
		float theta = center.x * Mathf.PI * 2f;
		float phi = center.y * Mathf.PI;
		Center3D = new Vector3(
			Mathf.Sin(phi) * Mathf.Cos(theta),
			Mathf.Cos(phi),
			Mathf.Sin(phi) * Mathf.Sin(theta)
		);
		
		// Random movement (will be used in later steps)
		float angle = Random.Range(0f, Mathf.PI * 2f);
		Speed = Random.Range(0.5f, 2.0f);
		MovementVector = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Speed;
		
		// Random crust type (will be used in heightmap generation)
		IsOceanic = Random.Range(0f, 1f) < config.oceanicPlateChance;
	}
}

/// <summary>
/// This is a "data class" that holds all the *output* data from the generator.
/// The PlanetGenerator *returns* this.
/// The PlanetController *uses* this to build the visuals.
/// </summary>
public class PlanetData
{
	// --- Store a copy of the inputs ---
	public PlanetGenerationConfig config;

	// --- Generated Profile (Outputs) ---
	public GeneratedPlanetType GeneratedType;
	public Color PlanetColor;
	public bool HasActiveGeology;
	public bool HasLiquidWater;
	public bool HasTectonics;
	public bool HasMagneticField;
	
	// --- MODIFIED ---
	// These are now RenderTextures, as they are generated and live on the GPU
	public RenderTexture PlateIDTexture; 
	
	// --- ADDED ---
	public RenderTexture Heightmap;

	// --- Tectonic Plate Data ---
	public List<TectonicPlate> TectonicPlates;
	
	// --- ADDED ---
	// GPU buffer to hold all plate data for compute shaders
	public ComputeBuffer TectonicPlatesBuffer;

	// --- Constructor ---
	public PlanetData(PlanetGenerationConfig config)
	{
		// Store the config that was used to create this data
		this.config = config;

		// Initialize all other properties to default values
		GeneratedType = GeneratedPlanetType.CratereWorld;
		PlanetColor = Color.grey;
		HasActiveGeology = false;
		HasLiquidWater = false;
		HasTectonics = false;
		HasMagneticField = false;
		TectonicPlates = new List<TectonicPlate>();
		
		// --- MODIFIED ---
		// Initialize all compute assets to null
		PlateIDTexture = null;
		Heightmap = null;
		TectonicPlatesBuffer = null;
	}

	/// <summary>
	/// Call this when destroying the planet to prevent memory leaks
	/// </summary>
	public void ReleaseComputeAssets()
	{
		// We must check if they exist before releasing them
		// .Release() is a method on RenderTexture and ComputeBuffer
		if (PlateIDTexture != null) PlateIDTexture.Release();
		if (Heightmap != null) Heightmap.Release();
		if (TectonicPlatesBuffer != null) TectonicPlatesBuffer.Release();
	}

	// --- Public Properties (Read-Only) ---
	public float PlanetRadius => config.planetRadius;
}

