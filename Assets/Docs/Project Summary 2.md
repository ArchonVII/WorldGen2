# **Project Handover: Procedural Planet Generator**

**Project Goal:** To create a high-performance, simulation-based procedural planet generator in Unity. The primary goal is not just to create a static planet, but to simulate its *evolution* over time, driven by tectonic plate movement.

## **1\. Current Status & Key Achievements**

The project has successfully transitioned from a CPU-based proof-of-concept to a high-performance, GPU-driven pipeline. This architecture is critical for all future simulation work.

* **GPU-Based Pipeline:** All heavy-lifting for texture generation is now done on the GPU using **Compute Shaders**. This has eliminated the main-thread freezes from C\# loops and is exceptionally fast.  
* **Phase 0 (Foundation) Complete:** We are successfully generating two core data textures *entirely on the GPU*:  
  1. PlateIDTexture (A RenderTexture map of the Voronoi-based tectonic plates).  
  2. Heightmap (A RenderTexture map of the "primordial" noise, representing the planet's initial lumpy surface).  
* **Robust Visualization:** We have a multi-layered visualization system for debugging and display:  
  * A 3D planet sphere.  
  * A multi-pass material overlay for crisp, anti-aliased plate boundaries (PlateBoundariesAA\_v2.shader).  
  * A second, transparent overlay to visualize the primordial noise map (PrimordialNoiseOverlay.shader).  
  * A 2D UI panel with RawImage components to display the raw texture data from the compute shaders directly.  
* **Deterministic Generation:** The entire planet's generation is deterministic, based on a seed derived from the planetAgeInBillions config parameter. This is a powerful feature for debugging and reproducibility. Toggling randomizeOnStart in the PlanetController will generate a new, unique planet on each run.

## **2\. Codebase Architecture Review**

The project is split into several key scripts and shaders, each with a distinct role.

### **Core C\# Scripts**

* **PlanetGenerationConfig (Struct):**  
  * **Role:** The "Input" configuration.  
  * This struct holds all user-facing parameters (e.g., planetAgeInBillions, numTectonicPlates, plateMapResolutionX). It is passed to the generator.  
* **PlanetData (Class):**  
  * **Role:** The "Output" data container.  
  * This class holds all the *results* of the generation.  
  * **Crucially, it holds the GPU assets:** RenderTextures (PlateIDTexture, Heightmap) and ComputeBuffer (TectonicPlatesBuffer).  
  * It contains the critical ReleaseComputeAssets() method to prevent GPU memory leaks in the editor.  
* **PlanetGenerator (Static Class):**  
  * **Role:** CPU-side setup and GPU resource creation.  
  * This class **does not** run any compute shaders. Its job is to:  
    1. Calculate the planet's high-level profile (e.g., HasTectonics).  
    2. Generate the *CPU-only* data (the List\<TectonicPlate\>).  
    3. Create the *empty* RenderTextures and the ComputeBuffer.  
    4. Load the CPU-side plate data into the ComputeBuffer to send it to the GPU.  
* **PlanetController (MonoBehaviour):**  
  * **Role:** The "GPU Orchestrator" and main runtime component.  
  * This class holds the references to the .compute shader assets.  
  * In Start(), it:  
    1. Calls PlanetGenerator.Generate() to get the PlanetData object.  
    2. **Dispatches** the compute shaders (RunPlateIDShader, RunPrimordialNoiseShader) to fill the empty textures.  
    3. Sets up the 3D planet mesh and 2D debug UI with the resulting textures.

### **Shaders & Compute**

* **GeneratePlateID.compute:**  
  * A compute shader that calculates the Voronoi diagram on the GPU. It reads plate data from the TectonicPlatesBuffer and writes the closest plate's ID to the PlateIDTexture.  
* **GeneratePrimordialNoise.compute:**  
  * A compute shader that uses the imported keijiro/NoiseShader library (ClassicNoise3D) to generate 3D fractal noise. This represents the planet's base "lumpiness."  
* **PlateBoundariesAA\_v2.shader:**  
  * A standard URP fragment shader that renders crisp, anti-aliased boundary lines by sampling the PlateIDTexture and checking for neighbors with different IDs.  
* **PrimordialNoiseOverlay.shader:**  
  * A simple unlit shader that renders the Heightmap as a grayscale overlay, used for debugging.

## **3\. Next Steps (The Simulation)**

The current "heightmap" is just noise. The next steps are to make this heightmap *simulation-driven*, using the tectonic plate data we've already generated.

### **Phase 1: Tectonic Uplift (Immediate Priority)**

This is the most important next step. We will create a new compute shader that generates a "Tectonic Modification Map" based on plate *interactions*.

1. **Create GenerateTectonicUplift.compute:**  
   * **Goal:** Create a new RenderTexture in PlanetData called TectonicModMap (format RFloat).  
   * **Inputs:** PlateIDTexture (to read) and TectonicPlatesBuffer (to read).  
   * **Logic (per pixel):**  
     1. Sample PlateIDTexture at the current pixel (myID).  
     2. Sample PlateIDTexture at neighboring pixels (e.g., neighborID).  
     3. **If myID \!= neighborID:** This pixel is on a boundary.  
     4. Fetch the PlateGpuData for both myID and neighborID from the TectonicPlatesBuffer.  
     5. Compare their MovementVectors relative to the boundary's direction.  
     6. Compare their isOceanic properties.  
     7. Based on these comparisons, write a modification value to TectonicModMap:  
        * **Convergent (Continent-Continent):** Write \+1.0 (Himalayas).  
        * **Convergent (Ocean-Continent):** Write \+0.7 on the continent side (Andes) and \-1.0 on the ocean side (Trench).  
        * **Divergent (Plates separating):** Write \-0.5 (Mid-Ocean Ridge / Rift Valley).  
        * **Transform (Sliding past):** Write \+0.1 (small hills/faults).  
     8. If not on a boundary, write 0.0.

### **Phase 2: Composite Planet Heightmap**

Create a final compute shader (CombineHeightmap.compute) to blend all our maps into the final Heightmap.

* **Inputs:** PrimordialNoiseMap, TectonicModMap, PlateIDTexture, TectonicPlatesBuffer.  
* **Logic (per pixel):**  
  1. Get the plate's baseHeight (e.g., isOceanic ? 0.2f : 0.5f).  
  2. Get the primordialNoise value.  
  3. Get the tectonicMod value.  
  4. float finalHeight \= baseHeight \+ (primordialNoise \* 0.1f) \+ tectonicMod;  
  5. Write this to the main Heightmap (or a new FinalHeightmap texture).  
* The PlanetController will then be updated to run this shader last and use its output for visualization.

### **Phase 3: Tectonic Evolution (The "Fast-Forward" Goal)**

This is the advanced simulation feature.

1. **Implement Texture Advection:** Create an Advect.compute shader.  
   * **Goal:** Move the data in our textures based on the plate vectors.  
   * **Logic:** This shader will create a *new* PlateIDTexture for the next timestep. For each pixel, it calculates *where it came from* based on its plate's MovementVector and samples the *old* PlateIDTexture at that previous location.  
2. **Create Simulation Loop:** The simulation loop (e.g., run every 10 million "years") would be:  
   1. Run Advect.compute to move the plates.  
   2. Run GenerateTectonicUplift.compute (to create new mountains/trenches at the *new* boundaries).  
   3. Run CombineHeightmap.compute (to get the final planet state).

## **4\. Considerations for Development**

* **GPU-Centric:** All large-scale texture generation **must** be done in compute shaders. Do not use C\# GetPixel/SetPixel loops; they are too slow.  
* **Data Flow:** The architecture is C\# \-\> ComputeBuffer \-\> ComputeShader \-\> RenderTexture \-\> Material. This is a one-way data flow to the GPU.  
* **Memory Management:** This is critical. Any RenderTexture or ComputeBuffer created in PlanetGenerator **must** be added to the PlanetData.ReleaseComputeAssets() method, which is called by PlanetController.OnDestroy(). Failure to do so *will* cause GPU memory leaks in the Unity Editor.  
* **Shader Debugging:** Compute shaders are difficult to debug. Use the 2D RawImage UI elements heavily. If your TectonicModMap looks wrong, immediately display it on the UI to see the raw data.