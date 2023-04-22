using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPH.Simulation
{
    internal class DFSPH : SPHMethod
    {
        public override void Step(Particle[] particles, Vector2[] external_accelerations, float timestep)
        {
            var positions = Array.ConvertAll(particles, particle => particle.Position);

            var neighbors = ComputeNeighbors(particles);

            var densities = new float[particles.Length];
            var alpha = new float[particles.Length];

            float rest_density = ComputeRestDensity();

            Parallel.For(0, particles.Length, ParallelOptions, i =>
            {
                if (particles[i].IsBoundary)
                {
                    densities[i] = SmoothingLength * rest_density;
                }
                else
                {
                    densities[i] = ComputeDensity(positions, neighbors, i);
                }
            });
        }
    }
}
