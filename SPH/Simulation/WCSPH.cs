using Microsoft.Xna.Framework;
using SPH.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPH.Simulation
{
    internal class WCSPH : SPHMethod
    {
        public override void Step(Particle[] particles, Vector2[] external_accelerations, float timestep)
        {
            var positions = Array.ConvertAll(particles, particle => particle.Position);
            var velocities = Array.ConvertAll(particles, particle => particle.Velocity);

            var densities = new float[particles.Length];
            var pressures = new float[particles.Length];

            var neighbors = ComputeNeighbors(particles);

            foreach (var particle in particles)
            {
                particle.Position += timestep / 2 * particle.Velocity;
            }

            float rest_density = ComputeRestDensity();

            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                if (particles[i].IsBoundary)
                {
                    densities[i] = SmoothingLength * rest_density;
                    pressures[i] = ComputePressure(densities[i], rest_density);
                }
                else
                {
                    densities[i] = ComputeDensity(positions, neighbors, i);
                    pressures[i] = ComputePressure(densities[i], rest_density);
                }
            });

            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                if (particles[i].IsBoundary)
                {
                    return;
                }

                Vector2 pressure_accel = ComputePressureAcceleration(positions, neighbors, densities, pressures, i);
                Vector2 viscosity_accel = ComputeViscosityAcceleration(positions, velocities, neighbors, densities, i);

                particles[i].Velocity += timestep * (pressure_accel + viscosity_accel + external_accelerations[i]);
                particles[i].Position += timestep / 2 * particles[i].Velocity;
            });
        }

    }
}
