using Microsoft.Xna.Framework;
using SPH.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPH.Simulation
{
    internal class PCISPH : SPHMethod
    {
        public float MaxDensityErrorFactor { get; set; } = 0.05f;
        public int MinIterations { get; set; } = 1;
        public int MaxIterations { get; set; } = 1;

        public override void Step(Particle[] particles, Vector2[] external_accelerations, float timestep)
        {
            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                particles[i].Position += timestep / 2 * particles[i].Velocity;
            });

            //Initialize algorithm state
            var predicted_positions = Array.ConvertAll(particles, particle => particle.Position);
            var predicted_velocities = Array.ConvertAll(particles, particle => particle.Velocity);

            var pressures = new float[particles.Length];
            var predicted_densities = new float[particles.Length];
            var pressure_accels = new Vector2[particles.Length];

            var neighbors = ComputeNeighbors(particles);

            float rest_density = ComputeRestDensity();
            float scaling_factor = ComputeScalingFactor(timestep, rest_density);

            //Compute initial densities before the main loop for viscosity calculation
            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                if (particles[i].IsBoundary)
                {
                    predicted_densities[i] = SmoothingLength / MathF.Sqrt(2) * rest_density;
                    pressures[i] = ComputePressure(predicted_densities[i], rest_density);
                }
                else
                {
                    predicted_densities[i] = ComputeDensity(predicted_positions, neighbors, i);
                }
            });

            //Compute viscosity as an external acceleration
            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                if(particles[i].IsBoundary)
                {
                    return;
                }

                external_accelerations[i] += ComputeViscosityAcceleration(
                        predicted_positions,
                        predicted_velocities,
                        neighbors,
                        predicted_densities,
                        i
                    );
            });

            float error_threshold = MaxDensityErrorFactor * rest_density;
            float max_error = 0;

            object max_error_lock = new();

            int it = 0;

            while((max_error > error_threshold || it < MinIterations) && it < MaxIterations)
            {
                //Predict state
                Parallel.For(0, particles.Length, ParallelOptions, i =>
                {
                    if (particles[i].IsBoundary)
                    {
                        return;
                    }

                    predicted_velocities[i] = particles[i].Velocity + timestep * (external_accelerations[i] + pressure_accels[i]);
                    predicted_positions[i] = particles[i].Position + timestep / 2 * predicted_velocities[i];
                });

                //Predict density and update pressure
                Parallel.For(0, particles.Length, ParallelOptions, i =>
                {
                    if (particles[i].IsBoundary)
                    {
                        return;
                    }

                    predicted_densities[i] = ComputeDensity(predicted_positions, neighbors, i);
                    float error = MathF.Max(0, predicted_densities[i] - rest_density);

                    lock(max_error_lock)
                    {
                        max_error = MathF.Max(max_error, error);
                    }

                    pressures[i] += scaling_factor * error;
                });

                //Update pressure acceleration
                Parallel.For(0, particles.Length, ParallelOptions, i =>
                {
                    if (particles[i].IsBoundary)
                    {
                        return;
                    }

                    pressure_accels[i] = ComputePressureAcceleration(predicted_positions, neighbors, predicted_densities, pressures, i);
                });

                it++;
            }

            //Integrate
            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                if (particles[i].IsBoundary)
                {
                    return;
                }

                particles[i].Velocity += timestep * (pressure_accels[i] + external_accelerations[i]);
                particles[i].Position += timestep / 2 * (particles[i].Velocity);
            });
        }

        private float ComputeScalingFactor(float timestep, float rest_density)
        {
            Vector2 grad_sum = Vector2.Zero;
            float grad_dot_sum = 0;

            for (float x = -SmoothingLength * 0.99f; x <= SmoothingLength + ParticleDistance / 2; x += ParticleDistance)
            {
                for (float y = -SmoothingLength * 0.99f; y <= SmoothingLength + ParticleDistance / 2; y += ParticleDistance)
                {
                    Vector2 pos = new Vector2(x, y);

                    if (pos.Length() < ParticleDistance / 2)
                    {
                        continue;
                    }

                    Vector2 grad = KernelGrad(pos, SmoothingLength);
                    grad_sum += grad;
                    grad_dot_sum += grad.Dot(grad);
                }
            }

            float beta = 2 * MathF.Pow(timestep * ParticleMass / rest_density, 2);
            return -1 / (beta * (-grad_sum.Dot(grad_sum) - grad_dot_sum));
        }
    }
}
