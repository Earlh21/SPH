using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using SPH.Simulation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SPH
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private Camera camera;
        private SPHMethod sph;

        private Particle[] particles;

        private bool started_panning = false;
        private int previous_scroll_value = 0;
        private Point start_mouse_pos = new Point(0, 0);

        private List<float> update_times = new();
        private Stopwatch stopwatch = new();
        bool running = false;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.GraphicsProfile = GraphicsProfile.HiDef;

            _graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;

            Content.RootDirectory = "Content";

            IsMouseVisible = true;
            Window.AllowUserResizing = true;
        }

        private IEnumerable<Particle> CreateCircle(float x, float y, float v_x, float v_y, float radius, float spacing)
        {
            for (float i = -radius; i <= radius; i += spacing)
            {
                for (float j = -radius; j <= radius; j += spacing)
                {
                    if (MathF.Sqrt(i * i + j * j) <= radius)
                    {
                        yield return new Particle(i + x, j + y, v_x, v_y, false);
                    }
                }
            }
        }

        private IEnumerable<Particle> CreateHorizontalBoundaryLine(float start_x, float y, float spacing, int length)
        {
            for(int i = 0; i < length; i++)
            {
                Vector2 pos = new(start_x + i * spacing, y);
                yield return new Particle(pos, Vector2.Zero, true);
            }
        }

        private IEnumerable<Particle> CreateVerticalBoundaryLine(float x, float start_y, float spacing, int height)
        {
            for (int i = 0; i < height; i++)
            {
                Vector2 pos = new(x, start_y + i * spacing);
                yield return new Particle(pos, Vector2.Zero, true);
            }
        }

        private IEnumerable<Particle> CreateHollowCircle(float x, float y, float radius, float spacing)
        {
            float arc_length = spacing / radius;

            for(float angle = 0; angle < 2 * MathF.PI; angle += arc_length)
            {
                yield return new Particle(MathF.Cos(angle) * radius + x, MathF.Sin(angle) * radius + y, 0, 0, true);
            }
        }

        protected override void Initialize()
        {
            float spacing = 1f;

            var particles_list = new List<Particle>();

            particles_list.AddRange(CreateHollowCircle(0, 0, 100, spacing));
            particles_list.AddRange(CreateHollowCircle(50, -30, 30, spacing));
            particles_list.AddRange(CreateHollowCircle(0, -70, 7, spacing));
            particles_list.AddRange(CreateHollowCircle(-30, -70, 7, spacing));
            particles_list.AddRange(CreateHollowCircle(30, -70, 7, spacing));
            particles_list.AddRange(CreateCircle(-40, 0, 6, 4, 30, spacing));

            particles = particles_list.ToArray();

            camera = new(Vector2.Zero, 4);

            var wcsph = new WCSPH
            {
                SmoothingLength = 2f * spacing,
                StiffnessY = 7,
                SoundSpeed = 20f,
                Viscosity = 0.3f,
                ParticleDistance = spacing,
                ParticleMass = 1,
                //ParallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 1}
            };

            var pcisph = new PCISPH
            {
                SmoothingLength = 2f * spacing,
                StiffnessY = 7,
                SoundSpeed = 20f,
                Viscosity = 0.1f,
                ParticleDistance = spacing,
                ParticleMass = 1,
                MinIterations = 1,
                MaxIterations = 1,
                MaxDensityErrorFactor = 0.05f
            };

            sph = pcisph;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            HandleTimeControls();
            HandleCameraControls();
            UpdatePhysics();

            base.Update(gameTime);
        }

        private void UpdatePhysics()
        {
            if (running)
            {
                var gravity = particles.Select(particle => new Vector2(0, -1)).ToArray();
                sph.Step(particles, gravity, 0.07f);
            }
        }

        private void HandleTimeControls()
        {
            if(Keyboard.GetState().IsKeyDown(Keys.Space))
            {
                running = true;
            }
        }

        private void HandleCameraControls()
        {
            if (Mouse.GetState().RightButton == ButtonState.Pressed && IsActive)
            {
                if (!started_panning)
                {
                    start_mouse_pos = Mouse.GetState().Position;
                    started_panning = true;
                }

                Point mouse_diff = Mouse.GetState().Position - start_mouse_pos;
                Vector2 mouse_diff_v = new Vector2(mouse_diff.X, mouse_diff.Y);
                camera.Center -= mouse_diff_v / camera.Zoom * 0.4f;
                Mouse.SetPosition(start_mouse_pos.X, start_mouse_pos.Y);

                IsMouseVisible = false;
            }
            else
            {
                started_panning = false;
                IsMouseVisible = true;
            }

            int scroll_value = Mouse.GetState().ScrollWheelValue;

            for (int i = 0; i < scroll_value - previous_scroll_value; i++)
            {
                camera.Zoom *= 1.001f;
            }

            for (int i = 0; i < previous_scroll_value - scroll_value; i++)
            {
                camera.Zoom /= 1.001f;
            }

            previous_scroll_value = scroll_value;
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            var batch = new SpriteBatch(GraphicsDevice);

            batch.Begin();

            float p_size = 0.5f * camera.Zoom;

            foreach (var particle in particles)
            {
                float size = p_size;
                float t = MathF.Min(particle.Velocity.Length() / 25, 1);
                var color = BlendColors(Color.Aquamarine, Color.Red, t);

                if(particle.IsBoundary)
                {
                    color = Color.Gray;
                    size *= 2;
                }

                batch.DrawCircle(camera.TransformToScreen(particle.Position, Window.ClientBounds), size, 12, color, size);
            }

            batch.End();

            base.Draw(gameTime);
        }

        private Color BlendColors(Color a, Color b, float t)
        {
            return new Color(a.R / 255f * (1 - t) + b.R / 255f * t, a.G / 255f * (1 - t) + b.G / 255f * t, a.B / 255f * (1 - t) + b.B / 255f * t);
        }
    }
}