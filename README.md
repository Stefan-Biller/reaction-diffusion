# ReactionDiffusion in Grasshopper - GPU-Accelerated Gray-Scott Simulation Component

A reaction-diffusion simulation in a Grasshopper Component or standalone Console App using GPU compute shaders via ComputeSharp.

## Features

- **GPU-accelerated**: Uses DirectX compute shaders via ComputeSharp
- **Real-time visualization**: See patterns emerge as the simulation runs
- **Configurable parameters**: Control diffusion rates, feed, and kill rates
- **Toroidal boundaries**: Seamless wrapping at edges
- **Batch processing**: Control update frequency for smooth animation

## Project Structure

```
reaction_diffusion/
├── ReactionDiffusion/              # Main Grasshopper component
│   ├── ReactionDiffusionComponent.cs
│   ├── ReactionDiffusionStep.cs    # GPU compute shader
│   └── ReactionDiffusionInfo.cs
├── ReactionDiffusion.Test/         # Standalone test application
│   ├── Program.cs
│   └── README.md
├── ReactionDiffusion.zip            # Precompiled component and dependencies
└── README.md                        # This file

```

## Requirements

### For Grasshopper Component

- Rhino 8
- Windows with DirectX 12 capable GPU
- .NET 8.0

### For Test Application

- .NET 8.0 SDK
- Windows with DirectX 12 capable GPU

## Installation

### Using in Grasshopper

1. Either use the provided files in the `ReactionDiffusion.zip` file.

   Or build the project:

   ```bash
   dotnet build -c Release
   ```

2. Copy `ReactionDiffusion.gha`, `ComputeSharp.Core.dll` and `ComputeSharp.dll` into a Grasshopper Components folder:

   ```
   %APPDATA%\Grasshopper\Libraries\
   ```

   or

   ```
   %APPDATA%\Grasshopper\Libraries\Simulation\
   ```

3. Restart Rhino/Grasshopper

### Running the Console Application

```bash
cd ReactionDiffusion.Console
dotnet run
```

See [ReactionDiffusion.Test/README.md](ReactionDiffusion.Console/README.md) for more details.

## Usage in Grasshopper

The component appears in **Simulation → Simulation** Tab.

### Pattern Presets

Common Feed/Kill combinations:

| Pattern | Feed   | Kill   | Description               |
| ------- | ------ | ------ | ------------------------- |
| Coral   | 0.0545 | 0.062  | Coral-like structures     |
| Mitosis | 0.0367 | 0.0649 | Cell division patterns    |
| Spots   | 0.029  | 0.057  | Spotted texture (default) |
| Stripes | 0.035  | 0.06   | Striped patterns          |
| Waves   | 0.014  | 0.054  | Wave-like formations      |

## Technical Details

### Algorithm

The Gray-Scott model simulates two virtual chemicals U and V:

```
dU/dt = Du·∇²U - UV² + F(1-U)
dV/dt = Dv·∇²V + UV² - (F+K)V
```

Where:

- `Du, Dv`: Diffusion rates
- `F`: Feed rate (adds U)
- `K`: Kill rate (removes V)
- `∇²`: Laplacian operator (3×3 convolution)

### GPU Shader

The compute shader (`ReactionDiffusionStep.cs`) processes all cells in parallel:

- Adjustable Thread group size
- Uses ping-pong buffers for double buffering
- Toroidal wrapping for seamless boundaries

### Building

```bash
dotnet build
```

## References

- [Gray-Scott Model](https://groups.csail.mit.edu/mac/projects/amorphous/GrayScott/)
- [Reaction-Diffusion Tutorial](http://karlsims.com/rd.html)
