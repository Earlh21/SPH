using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPH.Simulation
{
    internal class Particle
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public bool IsBoundary { get; set; }

        public Particle(Vector2 position, Vector2 velocity, bool is_boundary)
        {
            Position = position;
            Velocity = velocity;
            IsBoundary = is_boundary;
        }

        public Particle(float x, float y, float v_x, float v_y, bool is_boundary) : this(new Vector2(x, y), new Vector2(v_x, v_y), is_boundary)
        {

        }
    }
}
