# **Project: Procedural Planet Generator (Unity)**

**To the new developer:** Welcome\! This document outlines the current state of the project, its core architecture, and the planned next steps.

## **1\. Project Goal**

The primary goal of this project is to create a robust, procedural planet generator in Unity. The generator is built to be "plausible," meaning its results are not based on pure-physics simulation (which is too slow) but are not purely random, either. The aim is for a geologist to find the outputs believable at a high level.  
We are currently focused on generating "Earth-like" planets with liquid water and active plate tectonics.

## **2\. Current Architecture ("What We Have")**

We have established a clean, three-part **Separation of Concerns** architecture. Please follow this pattern:

### **a. PlanetController.cs (The "View")**

* This is a MonoBehaviour and the **only script** attached to a GameObject in the main scene.  
* **Its Job:**  
  * Holds all visual/scene-related settings (materials, camera speeds).  
  * Holds the **PlanetGenerationConfig struct** in its Inspector, which serves as the "master control panel" for all generation inputs.  
  * Manages the 3D camera (orbit/zoom) and auto-frames the planet.  
  * Manages the 2D debug UI (displaying generated maps).  
* **On Start():** It passes the config to the PlanetGenerator and receives a PlanetData object, which it then uses to build the scene.

### **b. PlanetGenerator.cs (The "Logic")**

* This is a **static class** (not a MonoBehaviour). It holds no state.  
* **Its Job:**  
  * Contains all the core generation algorithms.  
  * Its main method, Generate(PlanetGenerationConfig config), takes the inputs, runs all the math, and returns a new PlanetData object.

### **c. PlanetData.cs (The "Data")**

* This is a simple C\# file containing data structures.  
* **Its Job:**  
  * Defines all core **enum** types (HabitableZone, GeneratedPlanetType, MapResolution).  
  * Defines the **PlanetGenerationConfig struct**, which is our "input" blueprint.  
  * Defines the **PlanetData class**, which is our "output" blueprint. This class holds all the results of the generation (profile booleans, 2D maps, etc.).

## **3\. Current Features & State**

* **High-Level Logic:** The generator can successfully create four distinct planet types (EarthLike, VolcanicWorld, CratereWorld, IceShellWorld).  
* **Core Profile:** It determines a planet's type by first calculating a profile of boolean flags: HasActiveGeology, HasLiquidWater, and HasTectonics. These are derived from inputs like planetRadius, planetAgeInBillions, and waterAbundance.  
* **Phase 1: Tectonic Map:**  
  * We have successfully implemented the *first* 2D map: TectonicPlateMap.  
  * This map is a Texture2D showing the **boundaries** of tectonic plates.  
  * The algorithm is a **Voronoi noise generator** that creates plausible, cell-like shapes based on a numTectonicPlates input.  
* **Visualization:**  
  * The PlanetController generates a 3D sphere and a moon (if hasLargeMoon is true).  
  * The 3D planet's color is set based on its GeneratedPlanetType (e.g., blue for EarthLike, red for Volcanic).  
  * The generated TectonicPlateMap is displayed in a RawImage on the UI canvas for debugging.  
* **External Tools:**  
  * **FastNoise Lite** has been imported and is ready to be used.  
  * **Space Graphics Toolkit (SGT)** has been imported and is ready for future visualization upgrades.

## **4\. Planned Next Steps**

The current TectonicPlateMap is just the *start*. It only shows the *lines*. The next steps are to use these lines to build continents.

### **a. Step 1: Plate "Flood Fill" & Vectors (The Immediate Task)**

The TectonicPlateMap is just black and white. We need to identify *which* plate is which.

1. **Plate IDs:** Create a new map (e.g., Texture2D or int\[\]) where each pixel's value is the *ID* of the plate it belongs to. This is typically done with a "flood fill" or "region-labeling" algorithm starting from the Voronoi seed points.  
2. **Plate Data:** Create a List\<Plate\> in PlanetData. Each Plate object should store its ID and other properties.  
3. **Plate Vectors:** For each Plate, generate a random Vector2 for its direction and a float for its speed. This simulates the convection current.

### **b. Step 2: Heightmap & Continents (Using FastNoise)**

1. **Crust Type:** In PlateData, randomly assign each plate a type: **Oceanic** or **Continental**.  
2. **Generate Base Heightmap:** Create a new Texture2D called HeightMap.  
3. **Use FastNoise Lite** (e.g., Domain-Warped FBM) to generate a base noise pattern across the map.  
4. **Apply Crust Type:** Iterate through the HeightMap. Use the "Plate ID" map from Step 1 to check which plate a pixel belongs to.  
   * If the plate is **Continental**, add a large value (e.g., \+0.3) to its noise value.  
   * If the plate is **Oceanic**, subtract a value (e.g., \-0.2).  
   * This will instantly create the high-standing continents and low-lying ocean basins.

### **c. Step 3: Tectonic Simulation (Mountain Building)**

This is the "plausible geology" step.

1. **Analyze Boundaries:** Iterate through all pixels in the TectonicPlateMap (the white lines).  
2. **Check Neighbors:** For a given boundary pixel, check the **Plate ID** of its neighbors.  
3. **Compare Vectors:** Get the Vector (from Step 1\) for the two plates that are meeting.  
   * **Convergent (Collision):** If the vectors are pointing *towards* each other, this is a collision. Dramatically *raise* the HeightMap value along this boundary to form mountain ranges.  
   * **Divergent (Rift):** If the vectors are pointing *away* from each other, this is a rift. Dramatically *lower* the HeightMap value to form deep ocean ridges or rift valleys.  
   * **Transform (Shear):** If the vectors are sliding past each other, you can apply a "shear" effect using the noise (more advanced).

### **d. Step 4: Water, Erosion, & Visualization**

1. **Set Sea Level:** Use the waterAbundance variable to determine a global "sea level" (e.g., 0.5f).  
2. **Create WaterMap:** Any pixel on the HeightMap *below* this sea level is water.  
3. **Visualization:** Apply the HeightMap and WaterMap to the 3D sphere. This is the perfect time to integrate **Space Graphics Toolkit**, which has shaders designed to take a heightmap and a water map to render a beautiful, lit planet with real oceans.

## **5\. Additional Thoughts**

* **Architecture is Solid:** The Data/Logic/View separation is working well. Please continue to follow it. All new generation variables should go in PlanetGenerationConfig, new 2D maps should go in PlanetData, and new algorithms should go in PlanetGenerator.  
* **C\# Version:** The project is on **C\# 9.0** (standard for Unity LTS). This means you **cannot use field initializers in structs** (like PlanetGenerationConfig). All default values *must* be set in PlanetController.cs inside the GetDefaultConfig() and Reset() methods. This is already set up and working.  
* **FastNoise Lite:** This is a powerful tool. I recommend exploring its "Domain Warp" / "Cellular" features, as they are excellent for creating natural-looking continental shapes.