using ComputeSharp;

namespace ReactionDiffusion
{
    // 3x3 Laplacian-based Gray-Scott step with toroidal boundary conditions
    [ThreadGroupSize(256, 1, 1)]
    [GeneratedComputeShaderDescriptor]

    public readonly partial struct ReactionDiffusionStep : IComputeShader
    {
        public readonly ReadWriteBuffer<float> UIn;
        public readonly ReadWriteBuffer<float> VIn;
        public readonly ReadWriteBuffer<float> UOut;
        public readonly ReadWriteBuffer<float> VOut;

        public readonly int Width;
        public readonly int Height;
        public readonly float diffusionU;
        public readonly float diffusionV;
        public readonly float feed;
        public readonly float kill;
        public readonly float dt;

        /** the 3x3 convolution kernel for the Diffusion */
        float DiffusionKernel(
            float c, // center
            float n, float s, float e, float w, // cardinal neighbors
            float ne, float nw, float se, float sw) // diagonal neighbors
        {
            return
                0.20f * (n + s + e + w) +
                0.05f * (ne + nw + se + sw) -
                1.00f * c;
        }

        /** Get wrapped index for toroidal boundary conditions */
        private int GetWrappedIndex(int x, int y)
        {
            int wrappedX = ((x % Width) + Width) % Width;
            int wrappedY = ((y % Height) + Height) % Height;
            return wrappedY * Width + wrappedX;
        }

        public ReactionDiffusionStep(
            ReadWriteBuffer<float> uIn,
            ReadWriteBuffer<float> vIn,
            ReadWriteBuffer<float> uOut,
            ReadWriteBuffer<float> vOut,
            int width,
            int height,
            float diffusionU,
            float diffusionV,
            float feed,
            float kill,
            float dt)
        {
            UIn = uIn;
            VIn = vIn;
            UOut = uOut;
            VOut = vOut;
            Width = width;
            Height = height;
            this.diffusionU = diffusionU;
            this.diffusionV = diffusionV;
            this.feed = feed;
            this.kill = kill;
            this.dt = dt; // delta time
        }

        public void Execute()
        {
            int id = ThreadIds.X;
            int y = id / Width;
            int x = id % Width;

            // Diffusion using 3x3 Laplacian kernel 
            // cell neighbours wrap around boundaries (toroidal wrapping)
            // | nw  n  ne |
            // | w   c   e |
            // | sw  s  se |

            float u = UIn[id]; // current U concentration at center
            // Cardinal neighbors
            float n = UIn[GetWrappedIndex(x, y - 1)];
            float s = UIn[GetWrappedIndex(x, y + 1)];
            float e = UIn[GetWrappedIndex(x + 1, y)];
            float w = UIn[GetWrappedIndex(x - 1, y)];
            // Diagonal neighbors
            float ne = UIn[GetWrappedIndex(x + 1, y - 1)];
            float nw = UIn[GetWrappedIndex(x - 1, y - 1)];
            float se = UIn[GetWrappedIndex(x + 1, y + 1)];
            float sw = UIn[GetWrappedIndex(x - 1, y + 1)];

            float lapU = DiffusionKernel(u, n, s, e, w, ne, nw, se, sw);

            float v = VIn[id]; // current V concentration at center
            n = VIn[GetWrappedIndex(x, y - 1)];
            s = VIn[GetWrappedIndex(x, y + 1)];
            e = VIn[GetWrappedIndex(x + 1, y)];
            w = VIn[GetWrappedIndex(x - 1, y)];
            ne = VIn[GetWrappedIndex(x + 1, y - 1)];
            nw = VIn[GetWrappedIndex(x - 1, y - 1)];
            se = VIn[GetWrappedIndex(x + 1, y + 1)];
            sw = VIn[GetWrappedIndex(x - 1, y + 1)];

            float lapV = DiffusionKernel(v, n, s, e, w, ne, nw, se, sw);

            // Reaction term
            float reactionToV = u * v * v;

            float du = diffusionU * lapU - reactionToV + feed * (1f - u);
            float dv = diffusionV * lapV + reactionToV - (feed + kill) * v;

            UOut[id] = Hlsl.Clamp(u + du * dt, 0f, 1f);
            VOut[id] = Hlsl.Clamp(v + dv * dt, 0f, 1f);
        }
    }
}
