using Microsoft.Xna.Framework;
using SPH.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPH.Simulation
{
    internal class IISPH : SPHMethod
    {
        private Dictionary<Particle, float> previous_pressures = new();

        public override void Step(Particle[] particles, Vector2[] external_accelerations, float timestep)
        {
            var positions = Array.ConvertAll(particles, particle => particle.Position);
            var velocities = Array.ConvertAll(particles, particle => particle.Velocity);

            var densities = new float[particles.Length];
            var rho_adv = new float[particles.Length];
            var v_adv = new Vector2[particles.Length];
            var dii = new Vector2[particles.Length];
            var pressures = new float[particles.Length];

            var neighbors = ComputeNeighbors(particles);

            float rest_density = ComputeRestDensity();

            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                densities[i] = ComputeDensity(positions, neighbors, i);
            });

            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                Vector2 viscosity_accel = ComputeViscosityAcceleration
                (
                    positions,
                    velocities,
                    neighbors,
                    densities,
                    i
                );

                v_adv[i] = velocities[i] + timestep * (external_accelerations[i] + viscosity_accel);
                dii[i] = ComputeDii(positions, neighbors, densities, timestep, i);
            });

            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                rho_adv[i] = ComputeRhoAdv(positions, neighbors, densities, v_adv, timestep, i);
            });
        }

        private float ComputeRhoAdv(Vector2[] positions, List<int>[] neighbors, float[] densities, Vector2[] v_adv, float timestep, int i)
        {
            float sum = 0;

            foreach(var j in neighbors[i])
            {
                Vector2 v_ab = v_adv[j].To(v_adv[i]);
                sum += v_ab.Dot(KernelGrad(positions[j].To(positions[i]), SmoothingLength));
            }

            return densities[i] + timestep * ParticleMass * sum;
        }

        private Vector2 ComputeDii(Vector2[] positions, List<int>[] neighbors, float[] densities, float timestep, int i)
        {
            Vector2 sum = Vector2.Zero;

            foreach(var j in neighbors[i])
            {
                sum += -ParticleMass / (densities[i] * densities[i]) * KernelGrad(positions[j].To(positions[i]), SmoothingLength);
            }

            return timestep * timestep * sum;
        }
    }
}
