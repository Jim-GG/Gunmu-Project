# WindConeURP shader contract

`WindConeFX/URP/WindCone` is newly reconstructed URP HLSL. It is not the original game's shader source and does not use game assemblies. Intended integration: Unity 6000.3, Universal RP 17.3. The parent task performs actual Unity compile and visual validation; this staging file alone is not a successful compile claim.

## Source assets and sampling

- `_MainTex`: extracted `Eff_Smoke_075.png`, **green channel**. This is a packed RGB texture, not a directly viewable white smoke image. Its original material `_AlphaChannelMapping=1`; recovered shader property enum says R=0/G=1/B=2/A=3. `_MaskChannel=(0,1,0,0)` is consequently the evidence-based default.
- `_NoiseTex`: extracted `Eff_Noise_006.png`, red channel for warping/breakup. Green distortion uses an offset red sample to avoid relying on independent RG contents.
- Import both textures as **linear data** (`sRGBTexture=false`), Repeat wrap, bilinear/trilinear sampling. Suggested `_MainTex` scale `(2,1)`, `_NoiseTex` scale `(.5,.5)` reproduce the observed source material's tilings. These settings reproduce binding/tiling evidence, not the original shader's entire evaluation.
- Original `Eff_Cone_03` UV V grows with source local Z: Z=0 -> V=.002; Z=.39370078 -> V=.998. `_AxialUV=(0,1,0,0)`, `_AxialFlip=0`. Geometry reorientation does not require UV remapping. Original mesh is an open truncated/flared shell, not a solid pointed bullet.

## Renderer/controller properties

| Property | Type/default | Role |
|---|---|---|
| `_LifeAlpha` | float / 1 | Backward compatible `GameActions.DashWindConeController` lifetime envelope; zero hides emission and alpha |
| `_Opacity` | float / .72 | Additional overall opacity; MPB safe |
| `_Progress` | float / 0 | Optional erosion progress; 0 immediately visible, no start fade requirement; lifecycle must still fade `_LifeAlpha`/`_Opacity` to 0 |
| `_TintColor` | HDR Color | RGB emission tint, alpha multiplies opacity |
| `_Intensity` | float / 2.5 | HDR intensity multiplier |
| `_ScrollSpeed` | Vector / (0,-2.8,0,0) | UV scrolling in units per second |
| `_DistortionStrength` | float / .065 | Noise UV displacement |
| `_Additive` | float / .7 | 0 premultiplied transparency; 1 additive |
| `_AxialUV` / `_AxialFlip` | Vector / float | Original-UV axial fade direction |

One unlit pass, `UniversalPipeline`, `SRPDefaultUnlit`, transparent queue, `Blend One OneMinusSrcAlpha`, Cull Off, ZWrite Off, ZTest LEqual. At additive=1, output alpha is zero but premultiplied RGB still contains `_LifeAlpha * _Opacity`; the effect therefore fades out correctly even when fully additive. No depth texture, lights, postprocessing, particle-system custom streams, soft particles, renderer feature, or bloom dependency. Bloom enhances HDR but is not required to see it.

Vertex colors are optional and disabled by default to protect models imported without valid color streams. All scalar/vector material parameters are in `UnityPerMaterial`; textures and samplers are outside the CBUFFER. Per-renderer MaterialPropertyBlock updates are supported, but ordinary MPB use can disable SRP Batcher batching for the affected renderers.

## Reconstruction boundaries

Scrolling, noise distortion, smooth threshold breakup, non-scrolling axial fade, and mild two-sided grazing enhancement are authored reconstruction choices. Original game particle emission/lifetime curves, Custom Data, shader bytecode behavior, depth distortion, and scripts are not claimed recovered. Geometry/UV and the two input textures have extracted provenance; the shader is an editable reproduction using them.
