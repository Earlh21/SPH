using Microsoft.Xna.Framework;
using SPH.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SPH.Simulation
{
    internal abstract class SPHMethod
    {
        public float ParticleDistance { get; set; } = 1;
        public float SmoothingLength { get; set; } = 2;
        public float SoundSpeed { get; set; } = 88.5f;
        public float StiffnessY { get; set; } = 7;
        public float Viscosity { get; set; } = 0.08f;
        public float ParticleMass { get; set; } = 1;
        public ParallelOptions ParallelOptions { get; set; } = new ParallelOptions();

        public abstract void Step(Particle[] particles, Vector2[] external_accelerations, float timestep);

        protected float ComputePressure(float density, float rest_density)
        {
            return
                MathF.Max(0,
                    rest_density * SoundSpeed * SoundSpeed / StiffnessY *
                    (MathF.Pow(density / rest_density, StiffnessY) - 1));
        }

        protected float ComputeDensity(Vector2[] positions, List<int>[] neighbors, int i)
        {
            float sum = ParticleMass * Kernel(Vector2.Zero, SmoothingLength);

            foreach (var j in neighbors[i])
            {
                sum += ParticleMass * Kernel(positions[i].To(positions[j]), SmoothingLength);
            }

            return sum;
        }

        protected Vector2 ComputeViscosityAcceleration(Vector2[] positions, Vector2[] velocities, List<int>[] neighbors, float[] densities, int i)
        {
            Vector2 sum = Vector2.Zero;

            foreach (var j in neighbors[i])
            {
                Vector2 v_ab = velocities[i] - velocities[j];
                Vector2 x_ab = positions[i] - positions[j];
                float v_dot_x = v_ab.X * x_ab.X + v_ab.Y * x_ab.Y;

                if (v_dot_x > 0)
                {
                    continue;
                }

                float mu = 2.0f * Viscosity * SmoothingLength * SoundSpeed / (densities[i] + densities[j]);
                float PI_ab = -mu * (v_dot_x / (x_ab.LengthSquared() + 0.01f * SmoothingLength * SmoothingLength));
                sum += -ParticleMass * PI_ab * KernelGrad(x_ab, SmoothingLength);
            }

            return sum;
        }

        protected Vector2 ComputePressureAcceleration(Vector2[] positions, List<int>[] neighbors, float[] densities, float[] pressures, int i)
        {
            Vector2 sum = Vector2.Zero;

            foreach (var j in neighbors[i])
            {
                sum += ParticleMass *
                    (
                        pressures[i] / (densities[i] * densities[i]) +
                        pressures[j] / (densities[j] * densities[j])
                    ) * KernelGrad(positions[j].To(positions[i]), SmoothingLength);
            }

            return -ParticleMass * sum;
        }

        protected List<int>[] ComputeNeighbors(Particle[] particles)
        {
            Point GetGridPosition(Vector2 position)
            {
                return new Point(
                        (int)(position.X / SmoothingLength),
                        (int)(position.Y / SmoothingLength)
                    );
            }

            var grid = new Dictionary<Point, List<int>>();

            for (int i = 0; i < particles.Length; i++)
            {
                Point pos = GetGridPosition(particles[i].Position);

                if (!grid.ContainsKey(pos))
                {
                    grid[pos] = new();
                }

                grid[pos].Add(i);
            }


            var neighbors = new List<int>[particles.Length];

            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                neighbors[i] = new List<int>();

                if (particles[i].IsBoundary)
                {
                    return;
                }

                var pos = GetGridPosition(particles[i].Position);

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        var cell = grid.GetValueOrDefault(new Point(pos.X + x, pos.Y + y));

                        if (cell == null)
                        {
                            continue;
                        }

                        foreach (var j in cell)
                        {
                            if (i == j)
                            {
                                continue;
                            }

                            if (particles[i].Position.DistanceTo(particles[j].Position) > SmoothingLength)
                            {
                                continue;
                            }

                            neighbors[i].Add(j);
                        }
                    }
                }
            });

            return neighbors;
        }

        protected float ComputeRestDensity()
        {
            float density_sum = 0;

            for (float x = -SmoothingLength; x <= SmoothingLength + ParticleDistance / 2; x += ParticleDistance)
            {
                for (float y = -SmoothingLength; y <= SmoothingLength + ParticleDistance / 2; y += ParticleDistance)
                {
                    Vector2 pos = new Vector2(x, y);
                    density_sum += ParticleMass * Kernel(pos.To(Vector2.Zero), SmoothingLength);
                }
            }

            return density_sum;
        }

        protected float Kernel(Vector2 displacement, float smoothing_length)
        {
            float half_h = smoothing_length / 2;

            float k = 10 / (7 * MathF.PI) / (half_h * half_h);
            float q = displacement.Length() / half_h;

            return k * (q switch
            {
                < 1 => 1 - 1.5f * q * q + 0.75f * q * q * q,
                < 2 => 0.25f * MathF.Pow(2 - q, 3),
                _ => 0
            });
        }

        protected Vector2 KernelGrad(Vector2 displacement, float smoothing_length)
        {
            float dist = displacement.Length();
            Vector2 unit = displacement / dist;

            float half_h = smoothing_length / 2;

            float k = 10 / (7 * MathF.PI) / (half_h * half_h);
            float q = dist / half_h;

            return (q switch
            {
                < 1 => (k / half_h) * (-3 * q + 2.25f * q * q) * unit,
                < 2 => -0.75f * (k / half_h) * (2 - q) * (2 - q) * unit,
                _ => Vector2.Zero
            });
        }

        private static byte[] ToByteArray<T>(T[] source) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(source, GCHandleType.Pinned);
            try
            {
                IntPtr pointer = handle.AddrOfPinnedObject();
                byte[] destination = new byte[source.Length * Marshal.SizeOf(typeof(T))];
                Marshal.Copy(pointer, destination, 0, destination.Length);
                return destination;
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }

        private static T[] FromByteArray<T>(byte[] source) where T : struct
        {
            T[] destination = new T[source.Length / Marshal.SizeOf(typeof(T))];
            GCHandle handle = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try
            {
                IntPtr pointer = handle.AddrOfPinnedObject();
                Marshal.Copy(source, 0, pointer, source.Length);
                return destination;
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }
    }
}
