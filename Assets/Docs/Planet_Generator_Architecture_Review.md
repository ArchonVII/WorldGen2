# Planet Generator — Architecture Review & Next Steps

**Date:** 3 Nov 2025


## Where things live today (quick diagnosis)

- **Generation (CPU):** `PlanetGenerator.Generate(config)` builds a `PlanetData`, randomises (optional), decides the high‑level profile, creates plates, and allocates compute assets (ID map, heightmap, buffer). Good direction.  
  – Note: it currently seeds `Random` from `planetAgeInBillions * 1000`, which is deterministic but ties seed to “age” rather than a true seed field.  
  – GPU assets: Plate IDs as **R8**, heightmap as **RFloat**, wrap **Repeat**, random‑write enabled. Great.

- **Simulation (GPU, first passes):**  
  – Plate assignment compute: converts UV→sphere, picks **closest plate centre by max dot product**, writes an 8‑bit ID. Simple, correct, and scalable.  
  – Primordial noise compute: 3D FBM, layered octaves, writes 0–1 height. Solid base.

- **View/Orchestration:** `PlanetController` orchestrates: calls generator, dispatches both compute shaders, builds the sphere, adds **overlay materials to the same renderer** (nice fix vs the earlier second‑sphere issue), runs the camera orbit and **rotates the sun light**. It also frames the camera based on planet radius. All functional.

- **Boundary visual:** Unlit overlay shader samples the ID texture and detects boundaries via 8‑neighbour differences + `fwidth` AA. Clean and easy to tune, though it will alias on coarse maps and can look “pixelly” at sharp corners.


## Verdict

You’re *very* close to a clean separation. The only real confusion comes from names and mixed responsibilities inside `PlanetController`. I’d keep `PlanetGenerator` purely data‑oriented (already is), move view bits into a tiny “view” class, and peel camera/sun into their own rigs. That gives you small, safe surfaces for AI edits and easy diffs.


## Minimal, safe refactor (no behaviour change)

**Goal:** Pure data in `PlanetGenerator` → orchestration in `PlanetDirector` → rendering in `PlanetView` → independent rigs for camera & sun → tiny GPU pipeline wrapper.

1) **Rename & split `PlanetController` into 3 scripts** (keep fields/public API the same for now):

- `PlanetDirector : MonoBehaviour`  
  Orchestrates the whole flow: call generator, run compute pipeline, build the view, frame camera. (Move only the *orchestration* methods here.)

- `PlanetView` (plain C# or MB attached to the planet GO)  
  Owns the mesh/materials and *only* the “apply overlay materials / debug sphere / moon” code. (Rename `GeneratePlanet` → `BuildPlanetView` to reduce confusion.)

- `TectonicsComputePipeline`  
  Tiny wrapper that runs `RunPlateIDShader` and `RunPrimordialNoiseShader` given a `PlanetData`. (This makes AI edits less risky and keeps dispatch math in one place.)

2) **Peel the “rigs” out:**

- `OrbitalCameraRig : MonoBehaviour` (right‑drag orbit + zoom, looks at a target `Transform` supplied by `PlanetDirector`).  
- `SunRig : MonoBehaviour` (rotates the light around Y using a speed value). (This is already a clean method in your controller; just move it.)

3) **Nits while you’re there:**

- Add a `public int planetSeed` to `PlanetGenerationConfig`. Use that for `Random.InitState` (fall back to a derived value if zero). Today it’s keyed to age; let’s decouple.  
- Rename `GeneratePlanet()` → `BuildPlanetView()` to make “what happens where” obvious.  
- Keep `PlanetGenerator` *pure*: no scene knows about materials, lights, cameras. It only returns `PlanetData`. (You’ve already done 90% of this.)

> This refactor changes filenames and class names, not behaviour. You can do it in one commit and keep working immediately.


## Boundary lines: make them crisp and “analytic”

Today’s boundary detection is a **neighbour‑diff** in the fragment shader. It’s simple but inherently pixel‑based. Two cheap upgrades:

**A) “Second‑closest” trick in the compute pass (best quality):**  
In your plate ID compute, also compute the **2nd best dot**. Boundaries are where `(bestDot - secondDot)` is small. Write that delta into a small R16F texture (or pack into G of an RG16F). Then render **thin lines where delta < threshold** — resolution‑independent and beautifully crisp for the same map resolution. (Keeps overlay cost in the compute where it belongs.)

Pseudocode inside your loop:

```hlsl
float best=-2, second=-2; int bestID=0;
for (int i=0; i<_NumPlates; i++){
  float d = dot(p1, _PlateDataBuffer[i].center3D);
  if (d > best){ second=best; best=d; bestID=i; }
  else if (d > second){ second=d; }
}
float delta = best - second;
_PlateIDTexture[id.xy] = (float)bestID / 255.0;
_BoundaryDelta[id.xy] = delta; // new R16F
```

Then in the overlay frag:

```hlsl
float delta = SAMPLE_TEXTURE2D(_BoundaryDelta,...).r;
float alpha = smoothstep(threshold, threshold - aaw, delta); // invert because small=edge
```

**B) Supersample in the overlay shader (quick win):**  
Keep current approach but add a tiny 2× jittered supersample and average before `smoothstep`. This noticeably smooths diagonal lines on 2K maps.


## Plate motion: cheap, plausible initial vectors (no full mantle sim)

Your instinct’s right: we don’t need real convection. We just need **coherent, tangential vector fields** on the sphere so plates feel like they belong to large‑scale cells.

Add these now as options in `PlanetGenerationConfig` (even if only the first is used immediately):

```csharp
public enum PlateVelocityModel { Random, AxisFlows2, AxisFlows4, CurlNoise }
public int   planetSeed;
public float minPlateSpeed;  // ~0.2
public float maxPlateSpeed;  // ~2.0
public int   numConvectionAxes; // for AxisFlows
public float curlFrequency;  // for CurlNoise
```

**Model 1 — Axis flows (ultra cheap):**  
Pick N random unit axes `{a_k}` in 3D. The global velocity field is the *sum of solid‑body rotations* about those axes:  
`v(p) = Σ w_k * (a_k × p)` where `p` is the unit position on the sphere and `w_k` random weights.  
For each plate, sample `v` at `Center3D`, take its tangent (already tangent), normalize, scale to a speed in your `[min,max]`. Done. This yields believable great‑circle drifts and convergences.

**Model 2 — Curl noise on sphere (still cheap):**  
Sample low‑frequency 3D noise `N(p)` and set `v = curl(N)` projected to the tangent plane at `p`. Gives swirly cells and shear zones. (You already ship FBM infra on GPU; we can mirror a cheap CPU variant for seeding, or even generate a tiny 3D curl volume on the GPU and sample it for per‑plate vectors.)

**Model 3 — Neighbour rule tweaks (adds structure):**  
After assigning raw vectors, enforce light constraints:
- Ensure **X% oceanic plates** (you already randomise `IsOceanic`), and if two adjacent oceanics converge, mark one as “younger/denser → subductor” by biasing its vector slightly towards the other plate’s normal to make a trench‑like boundary later.
- Bias continental plates to **slower** speeds; oceanic plates slightly faster.

All of the above are constant‑time per plate and feel geologically “guided” without sim cost. You already have per‑plate fields for `Center3D`, `MovementVector`, `Speed`, `IsOceanic` — just compute and fill them more cleverly.


## Determinism & seeds (do this now)

- Add `planetSeed` to config and *always* `InitState(planetSeed)`. Keep your “randomize on start” path, but make it set both the seed and the other knobs so the whole planet is replayable. Right now the seed comes from age, which will cause surprises when you later want to scrub age/time.


## Performance runway (so we can keep adding detail)

- **Keep textures as the sim state.** You’re already building a strong pattern: “plate IDs” (R8), “height” (RFloat). Add more as needed:
  - `BoundaryDelta` (R16F) – see above
  - `CrustType` (R8U: oceanic/continental) – can derive from plate at first
  - `CrustAge` (R16F) – oceanic gets older with time steps (cheap increment)
  - `VelocityField` (RG16F) – only if/when you move to pixel‑level advection later

- **Use compute for the pixel stuff**, Jobs/Burst for sparse CPU bits (plate graph, future city placement). Dispatch sizes are already 8×8; that’s a good default.

- **Authoring vs playback resolution:** keep an internal “sim resolution” (e.g., 1024×512) and a “display resolution” (2K or 4K) you can upsample to. Your boundary approach B keeps crispness; approach A is even better.

- **PlateID > 255?** You currently cap effectively at 256 (R8). That’s fine for now (you default to ~12 plates). If you *ever* want more, flip the ID target to `GraphicsFormat.R16_UInt` and write a uint. (Not urgent.)


## Small correctness nits & UX

- **Camera framing:** Your `FramePlanet()` scales distance by planet radius and sets FOV 45°; that’s good for a wide range of radii and should fix “too zoomed” complaints after earlier changes.
- **Overlay rotation bug (old):** You’ve already moved to stacking materials on the main planet mesh, so overlays rotate correctly. Keep that.
- **Dispose GPU stuff:** You already call `ReleaseComputeAssets()` in `OnDestroy()`. Great — remember to call it on regenerate too if you add a “Regenerate” button.


## Add‑now variables that unlock later work (even if unused today)

Add these to `PlanetGenerationConfig` so they flow through saves/UI and don’t require breaking changes later:

- `int planetSeed`  
- `PlateVelocityModel velocityModel`  
- `float minPlateSpeed, maxPlateSpeed, curlFrequency`  
- `float erosionRate, upliftRate` *(placeholders for later)*  
- `int simResolutionX, simResolutionY` *(alias your map resolutions for clarity)*  
- `float timeScaleMyrPerSec` *(so we can scrub “500 Myr” moments visibly)*


## Concrete next steps (bite‑sized, low risk)

1) **Add seed + velocity model** to config; compute plate vectors via **AxisFlows2** as default (10–15 lines of CPU code). Fill `MovementVector/Speed` using this instead of pure random.  
2) Split **`PlanetController`** into `PlanetDirector` + `PlanetView` + `TectonicsComputePipeline` + `SunRig` + `OrbitalCameraRig` (file/class renames mostly). Keep all public fields the same so the scene continues to run.  
3) **Boundary Delta**: add the second‑closest dot in the plate compute and a tiny change to the overlay to use it. (Or apply the quick 2× supersample for a 5‑minute stopgap.)  
4) Add a **“Sim Resolution”** dropdown (512/1k/2k) that controls the textures you allocate today (ID/height). Your compute dispatch already generalises.  
5) Plumb a **Regenerate** button that rebuilds `PlanetData`, re‑dispatches the pipeline, and reuses `PlanetView`. Remember to release old compute buffers.

If you’d like, I can draft the tiny AxisFlows plate‑vector code and the “second‑closest” boundary compute pass next, then give you the file‑by‑file diffs for the class splits so you can paste them in without collateral edits.
