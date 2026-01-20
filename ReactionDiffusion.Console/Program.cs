using System;
using System.IO;
using ComputeSharp;
using SkiaSharp;

// TESTFILE WRITTEN BY LLM

const int width = 128;
const int height = 128;
const int steps = 1500;
int size = width * height;

float[] u = new float[size];
float[] v = new float[size];

// Initialize: U=1 everywhere, V=0 everywhere
Array.Fill(u, 1.0f);
Array.Fill(v, 0.0f);

// Add noise to V to generate initial patterns (matching component initialization)
int seed = 1;
var rng = new Random(seed);
var noiseAmp = 0.2f;
for (int i = 0; i < size; i++)
{
  float noise = (float)(rng.NextDouble() * 2 - 1) * noiseAmp;
  v[i] = Math.Clamp(v[i] + noise, 0f, 1f);
}

// Default pattern parameters from component
float diffusionU = 0.16f;
float diffusionV = 0.08f;
float feed = 0.029f;
float kill = 0.057f;
float dt = 1.0f;

var device = GraphicsDevice.GetDefault();

var uA = device.AllocateReadWriteBuffer(u);
var vA = device.AllocateReadWriteBuffer(v);
var uB = device.AllocateReadWriteBuffer<float>(size);
var vB = device.AllocateReadWriteBuffer<float>(size);

Console.WriteLine($"Running {steps} steps on {width}×{height} grid...");
var sw = System.Diagnostics.Stopwatch.StartNew();

for (int i = 0; i < steps; i++)
{
  device.For(size, new ReactionDiffusion.ReactionDiffusionStep(
    uA, vA, uB, vB,
    width, height,
    diffusionU: diffusionU,
    diffusionV: diffusionV,
    feed: feed,
    kill: kill,
    dt: dt));

  // Swap buffers for next iteration
  (uA, uB) = (uB, uA);
  (vA, vB) = (vB, vA);

  Console.WriteLine($"  Step {i + 1}/{steps}");
}

sw.Stop();
Console.WriteLine($"Completed in {sw.ElapsedMilliseconds}ms");

// Read back results (from the final output buffer after swapping)
var vResult = (steps % 2 == 1) ? vA.ToArray() : vB.ToArray();

// Dispose buffers
uA.Dispose();
vA.Dispose();
uB.Dispose();
vB.Dispose();

// Save as PNG using SkiaSharp
using var bitmap = new SKBitmap(width, height);
for (int y = 0; y < height; y++)
{
  for (int x = 0; x < width; x++)
  {
    float val = vResult[y * width + x];
    byte gray = (byte)(Math.Clamp(val, 0f, 1f) * 255);
    bitmap.SetPixel(x, y, new SKColor(gray, gray, gray));
  }
}

string outputPath = Path.Combine(Environment.CurrentDirectory, "reaction_diffusion_output.png");
using var image = SKImage.FromBitmap(bitmap);
using var data = image.Encode(SKEncodedImageFormat.Png, 100);
using var stream = File.OpenWrite(outputPath);
data.SaveTo(stream);
Console.WriteLine($"Saved to: {outputPath}");
