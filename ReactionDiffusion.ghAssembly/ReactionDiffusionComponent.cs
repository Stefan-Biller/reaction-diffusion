using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using ComputeSharp;
using Grasshopper.Kernel.Types;
using System.DirectoryServices.ActiveDirectory;


namespace ReactionDiffusion
{
    public class ReactionDiffusionComponent : GH_Component
    {
        public ReactionDiffusionComponent()
          : base("ReactionDiffusion Component", "ReactionDiffusion",
            "Run a reaction-diffusion simulation using Gray-Scott model on the GPU.",
            "Simulation", "Simulation")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "R", "Run Simulation", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Reset", "RS", "Reset Simulation", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Cells X", "X", "Number of cells in x direction", GH_ParamAccess.item, 128);
            pManager.AddIntegerParameter("Cells Y", "Y", "Number of cells in y direction", GH_ParamAccess.item, 128);
            pManager.AddNumberParameter("Diffusion U", "Du", "Diffusion rate of U [0.0-1.0], usually [0.05 - 0.25]", GH_ParamAccess.item, 0.16);
            pManager.AddNumberParameter("Diffusion V", "Dv", "Diffusion rate of V [0.0-1.0], usually [0.02 - 0.12]", GH_ParamAccess.item, 0.08);
            pManager.AddNumberParameter("Feed Rate", "F", "Feed rate [0.0-1.0], usually  [0.01 - 0.08]", GH_ParamAccess.item, 0.029);
            pManager.AddNumberParameter("Kill Rate", "K", "Kill rate [0.0-1.0], usually  [0.045 - 0.070]", GH_ParamAccess.item, 0.057);
            pManager.AddNumberParameter("Time Step", "dt", "Time step for integration, should usually be 1.0, decrease for more stability", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("Steps", "S", "Total simulation steps", GH_ParamAccess.item, 1500);
            pManager.AddIntegerParameter("OutputSteps", "OS", "Output simulation state every N steps. Increasing this will significantly improve performance. Set to 0 to output only final step", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("Seed", "Seed", "Seed for initial random noise generation", GH_ParamAccess.item, 1);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("U", "U", "Linearized U Results for each cell", GH_ParamAccess.list);
            pManager.AddNumberParameter("V", "V", "Linearized V Results for each cell", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Current Step", "S", "Current simulation step", GH_ParamAccess.item);
        }

        // setup values - these cannot be changed during a running simulation
        private class SimulationState
        {
            public GraphicsDevice device;
            public ReadWriteBuffer<float> uA;
            public ReadWriteBuffer<float> vA;
            public ReadWriteBuffer<float> uB;
            public ReadWriteBuffer<float> vB;
            public int width;
            public int height;
            public int currentStep;
            public int targetSteps;
            public float diffusionU;
            public float diffusionV;
            public float feed;
            public float kill;
            public float timeStep;
            public int seed;

            public List<GH_Number> outputU;
            public List<GH_Number> outputV;
        }

        private SimulationState State = null;

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            bool reset = false;
            int width = 128;
            int height = 128;
            double diffU_d = 0.16;
            double diffV_d = 0.08;
            double feed_d = 0.0367;
            double kill_d = 0.0649;
            double timeStep_d = 1.0;
            int seed = 1;
            int totalSteps = 1500;
            int updateInterval = 10;

            DA.GetData("Run", ref run);
            DA.GetData("Reset", ref reset);
            DA.GetData("Cells X", ref width);
            DA.GetData("Cells Y", ref height);
            DA.GetData("Diffusion U", ref diffU_d);
            DA.GetData("Diffusion V", ref diffV_d);
            DA.GetData("Feed Rate", ref feed_d);
            DA.GetData("Kill Rate", ref kill_d);
            DA.GetData("Time Step", ref timeStep_d);
            DA.GetData("Seed", ref seed);
            DA.GetData("Steps", ref totalSteps);
            DA.GetData("OutputSteps", ref updateInterval);

            // input validation
            var warnings = new List<string>();
            if (width < 3) { warnings.Add("Set width to minimum of 3 cells."); width = 3; }
            if (height < 3) { warnings.Add("Set height to minimum of 3 cells."); height = 3; }
            if (diffU_d < 0.00 || diffU_d > 1.00) warnings.Add("Diffusion U out of range [0.00 - 1.00]. Clamped.");
            float diffU = Math.Clamp((float)diffU_d, 0.00f, 1.00f);
            if (diffV_d < 0.00 || diffV_d > 1.00) warnings.Add("Diffusion V out of range [0.00 - 1.00]. Clamped.");
            float diffV = Math.Clamp((float)diffV_d, 0.00f, 1.00f);
            if (feed_d < 0.00 || feed_d > 1.00) warnings.Add("Feed Rate out of range [0.00 - 1.00]. Clamped.");
            float feed = Math.Clamp((float)feed_d, 0.00f, 1.00f);
            if (kill_d < 0.00 || kill_d > 1.00) warnings.Add("Kill Rate out of range [0.00 - 1.00]. Clamped.");
            float kill = Math.Clamp((float)kill_d, 0.00f, 1.00f);
            if (timeStep_d < 0.001 || timeStep_d > 10.0) warnings.Add("Time Step out of range [0.001 - 10.0]. Clamped.");
            float timeStep = Math.Clamp((float)timeStep_d, 0.001f, 10.0f);
            if (totalSteps < 0) { warnings.Add("Set Steps to minimum of 0."); totalSteps = 0; }
            if (updateInterval > totalSteps) { warnings.Add("OutputSteps > Steps. Setting to Steps."); updateInterval = totalSteps; }
            if (updateInterval < 1) { updateInterval = totalSteps; } // 0 for final step output only
            if (updateInterval > totalSteps) { updateInterval = totalSteps; }

            foreach (var warning in warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warning);

            // Output current state if not running
            if (!run)
            {
                DA.SetDataList("U", State?.outputU ?? new List<GH_Number>(width * height));
                DA.SetDataList("V", State?.outputV ?? new List<GH_Number>(width * height));
                DA.SetData("Current Step", State?.currentStep ?? 0);
                return;
            }

            // Handle reset
            if (reset)
            {
                DisposeSimulation();
                DA.SetDataList("U", new List<GH_Number>(width * height));
                DA.SetDataList("V", new List<GH_Number>(width * height));
                DA.SetData("Current Step", 0);
                return;
            }

            // Re-/Initialize if needed
            bool inputsChanged =
                State?.width != width ||
                State?.height != height ||
                State?.diffusionU != diffU ||
                State?.diffusionV != diffV ||
                State?.feed != feed ||
                State?.kill != kill ||
                State?.timeStep != timeStep ||
                State?.seed != seed ||
                State?.targetSteps != totalSteps;
            if (State == null || inputsChanged)
            {
                DisposeSimulation();
                try
                {
                    SetupSimulation(width, height, diffU, diffV, feed, kill, timeStep, seed, totalSteps);
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Init failed: {ex.Message}");
                    return;
                }
            }

            // Run batch of simulation steps
            if (State.currentStep < State.targetSteps)
            {
                int size = State.width * State.height;
                int stepsToRun = Math.Min(updateInterval, State.targetSteps - State.currentStep);

                for (int i = 0; i < stepsToRun; i++)
                {
                    // this reads the input buffer updates every cell of the target buffer
                    State.device.For(size, new ReactionDiffusionStep(
                        State.uA, State.vA, State.uB, State.vB,
                        State.width, State.height,
                        State.diffusionU, State.diffusionV,
                        State.feed, State.kill,
                        State.timeStep));

                    // toggle the read and write buffer for the next step
                    (State.uA, State.uB) = (State.uB, State.uA);
                    (State.vA, State.vB) = (State.vB, State.vA);

                    State.currentStep++;
                }

                // Update component state
                var uData = State.uA.ToArray();
                var vData = State.vA.ToArray();
                State.outputU = uData.Select(val => new GH_Number(val)).ToList();
                State.outputV = vData.Select(val => new GH_Number(val)).ToList();
            }

            // Output current state
            DA.SetDataList("U", State.outputU ?? []);
            DA.SetDataList("V", State.outputV ?? []);
            DA.SetData("Current Step", State.currentStep);

            // Display progress message
            float progressPercent = State.targetSteps > 0
                ? (float)State.currentStep / State.targetSteps * 100f
                : 0f;
            Message = $"{progressPercent:F0}%";

            // Expire solution if more steps remain
            // Recomputation will trigger next batch of steps
            if (State.currentStep < State.targetSteps)
            {
                ExpireSolution(true);
            }
        }

        private void SetupSimulation(int width, int height, float diffU, float diffV, float feed, float kill, float timeStep, int seed, int targetSteps)
        {
            // uses flat arrays for optimized GPU buffer transfer and throughput
            int size = width * height;
            float[] u = new float[size];
            float[] v = new float[size];
            Array.Fill(u, 1.0f);
            Array.Fill(v, 0.0f);

            // add some noise to generate initial patterns
            var rng = new Random(seed);
            var noiseAmp = 0.2f;
            for (int i = 0; i < size; i++)
            {
                float noise = (float)(rng.NextDouble() * 2 - 1) * noiseAmp;
                v[i] = Math.Clamp(v[i] + noise, 0f, 1f);
            }

            // Select GPU with most cores - proxy for best performance
            var device = GraphicsDevice
                .EnumerateDevices()
                .OrderByDescending(d => d.ComputeUnits)
                .First();
            State = new SimulationState
            {
                device = device,
                uA = device.AllocateReadWriteBuffer(u),
                vA = device.AllocateReadWriteBuffer(v),
                uB = device.AllocateReadWriteBuffer<float>(size),
                vB = device.AllocateReadWriteBuffer<float>(size),
                width = width,
                height = height,
                currentStep = 0,
                targetSteps = targetSteps,
                diffusionU = diffU,
                diffusionV = diffV,
                feed = feed,
                kill = kill,
                timeStep = timeStep,
                seed = seed,
                outputU = u.Select(val => new GH_Number(val)).ToList(),
                outputV = v.Select(val => new GH_Number(val)).ToList()
            };
        }

        private void DisposeSimulation()
        {
            if (State == null) return;

            State.uA.Dispose();
            State.vA.Dispose();
            State.uB.Dispose();
            State.vB.Dispose();
            State = null;
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            DisposeSimulation();
            base.RemovedFromDocument(document);
        }

        // add a simple preview of V values
        private class PreviewSettings
        {
            public Plane plane = Plane.WorldXY;
            public Mesh mesh = null;
            public string meshHash = "";
            public int currentStep = -1;
            public float cellSize = 1.0f;
            public Rhino.Display.DisplayMaterial material = new Rhino.Display.DisplayMaterial(Color.White);
        }
        private PreviewSettings previewSettings = new PreviewSettings();

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (Hidden) return;
            if (State == null || State.outputV == null) return;

            // Create a colored mesh based on V values
            int width = State.width;
            int height = State.height;

            Mesh mesh = previewSettings.mesh;

            // Rebuild mesh if size changed
            string hash = $"{width}.{height}";
            if (mesh == null || previewSettings.meshHash != hash)
            {
                previewSettings.meshHash = hash;
                previewSettings.mesh = new Mesh();
                previewSettings.currentStep = -1; // force color update
                mesh = previewSettings.mesh;

                var plane = previewSettings.plane;
                var cellSize = previewSettings.cellSize;

                // Create vertices
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Point3d pt = plane.PointAt(x * cellSize, y * cellSize);
                        mesh.Vertices.Add(pt);
                    }
                }

                // Create faces
                for (int y = 0; y < height - 1; y++)
                {
                    for (int x = 0; x < width - 1; x++)
                    {
                        int i = y * width + x;
                        mesh.Faces.AddFace(i, i + 1, i + width + 1, i + width);
                    }
                }
            }

            // update colors if simulation step changed, color based on V values (grayscaleq)
            if (previewSettings.currentStep != State.currentStep)
            {
                // could optimize by reusing existing colors
                previewSettings.mesh.VertexColors.Clear();

                // Find min and max for normalization
                float minV = float.MaxValue;
                float maxV = float.MinValue;
                for (int i = 0; i < State.outputV.Count; i++)
                {
                    float v = (float)State.outputV[i].Value;
                    minV = Math.Min(minV, v);
                    maxV = Math.Max(maxV, v);
                }
                Interval bounds = new Interval(minV, maxV);

                for (int i = 0; i < State.outputV.Count; i++)
                {
                    // reconversion from GH_Number to float is not optimal 
                    // but imo acceptable for fast preview
                    var val = bounds.NormalizedParameterAt(State.outputV[i].Value);
                    int tone = (int)(val * 255);
                    previewSettings.mesh.VertexColors.Add(Color.FromArgb(tone, tone, tone));
                }

                previewSettings.currentStep = State.currentStep;
            }

            args.Display.DrawMeshFalseColors(mesh);
        }
        public override BoundingBox ClippingBox
        {
            get
            {
                if (State == null) return BoundingBox.Empty;

                var plane = previewSettings.plane;
                float cellSize = previewSettings.cellSize;

                BoundingBox box = new BoundingBox(
                    Point3d.Origin,
                    plane.PointAt(State.width * cellSize, State.height * cellSize)
                );
                return box;
            }
        }

        public override bool IsPreviewCapable => true;




        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("111c6e19-929e-40fe-91c4-8c0ec648ad23");
    }
}