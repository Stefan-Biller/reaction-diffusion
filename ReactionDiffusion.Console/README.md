# ReactionDiffusion.Test

LLM generated testcode to run the reaction-diffusion shader in isolation, outside of Rhino/Grasshopper. It uses ComputeSharp to run the simulation on the GPU and saves the output as a PNG image.

## Requirements

- .NET 8.0 SDK
- Windows (for DirectX/GPU support)
- GPU with DirectX 12 support

## How to Run

```bash
cd ReactionDiffusion.Test
dotnet run
```

## Output

The program generates `reaction_diffusion_output.png` in the output directory

## Configuration

You can modify the simulation parameters in `Program.cs`:

```csharp
const int width = 128;          // Grid width
const int height = 128;         // Grid height
const int steps = 1500;         // Number of simulation steps

float diffusionU = 0.16f;       // U diffusion rate
float diffusionV = 0.08f;       // V diffusion rate
float feed = 0.029f;            // Feed rate
float kill = 0.057f;            // Kill rate
float dt = 1.0f;                // Time step
```

## Pattern Presets

Known parameter sets for common patterns:

| Pattern | Feed   | Kill   |
| ------- | ------ | ------ |
| Coral   | 0.0545 | 0.062  |
| Mitosis | 0.0367 | 0.0649 |
| Spots   | 0.029  | 0.057  |
| Stripes | 0.035  | 0.06   |
| Waves   | 0.014  | 0.054  |
