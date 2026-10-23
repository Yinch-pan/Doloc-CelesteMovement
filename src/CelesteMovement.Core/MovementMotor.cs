using System;

namespace Doloc.CelesteMovement.Core
{
    // Celeste's ordinary movement rules, expressed with positive Y pointing UP.
    // No game assemblies, assets or decompiled method bodies are shipped here.
    public struct MotionInput
    {
        public int X, Y;
        public bool JumpPressed, JumpHeld, DashPressed;
    }

    public struct MotionEnvironment
    {
        public bool Grounded, WallLeft, WallRight, Ceiling;
    }

    public sealed class MovementMotor
    {
        public const float RunSpeed = 90f;
        public const float JumpSpeed = 105f;
        public const float DashSpeed = 240f;
        public float X { get; private set; }
        public float Y { get; private set; }
        public int Facing { get; private set; } = 1;
        public bool HasDash { get; private set; } = true;
        public bool Dashing => dashTime > 0;
        public bool Jumped { get; private set; }
        public bool DashStarted { get; private set; }
        public bool WallJumped { get; private set; }
        public bool EnableWallJump { get; set; } = true;
        public float CoyoteSeconds { get; set; } = .1f;
        public float InputBufferSeconds { get; set; } = .1f;
        private float coyote, jumpBuffer, dashBuffer, variableJump, dashTime, dashCooldown, refillCooldown;
        private float variableSpeed, dashX, dashY, fallLimit = 160f, forcedTime, wallSlideTime = 1.2f;
        private int forcedX;
        private bool hasSimulated;

        public void Reset(float x, float y, bool refillDash = true, int facing = 1)
        {
            X = x; Y = y; Facing = facing < 0 ? -1 : 1;
            coyote = jumpBuffer = dashBuffer = variableJump = dashTime = dashCooldown = refillCooldown = forcedTime = 0;
            fallLimit = 160f; wallSlideTime = 1.2f; hasSimulated = false;
            Jumped = DashStarted = WallJumped = false;
            if (refillDash) HasDash = true;
        }

        public void ClearInputBuffers() { jumpBuffer = dashBuffer = 0; }

        // Feed back the collision solver's result once per physics tick.
        public void ObserveVelocity(float x, float y)
        {
            if (hasSimulated && Y > 0 && y <= 0) variableJump = 0;
            X = x; Y = y;
        }

        public void Step(float dt, MotionInput input, MotionEnvironment env)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            dt = Math.Min(dt, .05f);
            Jumped = DashStarted = WallJumped = false;
            input.X = Math.Sign(input.X); input.Y = Math.Sign(input.Y);
            if (input.JumpPressed) jumpBuffer = InputBufferSeconds;
            if (input.DashPressed) dashBuffer = InputBufferSeconds;
            dashCooldown = Down(dashCooldown, dt);
            refillCooldown = Down(refillCooldown, dt);
            forcedTime = Down(forcedTime, dt);
            if (env.Grounded)
            {
                coyote = CoyoteSeconds; wallSlideTime = 1.2f;
                if (!Dashing && refillCooldown <= 0) HasDash = true;
            }
            else coyote = Down(coyote, dt);

            if (Dashing)
            {
                // Collision responses are authoritative; do not continually push a blocked axis.
                if ((dashX < 0 && env.WallLeft) || (dashX > 0 && env.WallRight)) X = 0;
                if ((dashY > 0 && env.Ceiling) || (dashY < 0 && env.Grounded)) Y = 0;
                dashTime = Down(dashTime, dt);
                if (!Dashing)
                {
                    if (dashY >= 0)
                    {
                        X = X == 0 ? 0 : dashX * 160f;
                        Y = Y == 0 ? 0 : dashY * 160f;
                        if (Y > 0) Y *= .75f;
                    }
                }
                Finish(dt);
                return;
            }

            if (dashBuffer > 0 && HasDash && dashCooldown <= 0)
            {
                int aimX = input.X, aimY = input.Y;
                if (aimX == 0 && aimY == 0) aimX = Facing;
                float norm = aimX != 0 && aimY != 0 ? .70710678118f : 1f;
                dashX = aimX * norm; dashY = aimY * norm;
                float launchX = dashX * DashSpeed;
                if (Math.Sign(X) == Math.Sign(launchX) && Math.Abs(X) > Math.Abs(launchX)) launchX = X;
                X = launchX; Y = dashY * DashSpeed;
                if (aimX != 0) Facing = aimX;
                dashTime = .15f; dashCooldown = .2f; refillCooldown = .1f;
                dashBuffer = variableJump = coyote = 0;
                HasDash = false; DashStarted = true;
                Finish(dt);
                return;
            }

            int move = forcedTime > 0 ? forcedX : input.X;
            if (move != 0) Facing = move;
            float air = env.Grounded ? 1f : .65f;
            float acceleration = Math.Abs(X) > RunSpeed && Math.Sign(X) == move ? 400f : 1000f;
            X = Approach(X, move * RunSpeed, acceleration * air * dt);
            float targetFall = input.Y < 0 && Y <= -160f ? 240f : 160f;
            fallLimit = Approach(fallLimit, targetFall, 300f * dt);
            if (env.Grounded)
            {
                if (Y < 0) Y = 0;
            }
            else
            {
                float limit = fallLimit;
                bool slide = input.Y >= 0 && Y <= 0 && wallSlideTime > 0 &&
                    ((input.X < 0 && env.WallLeft) || (input.X > 0 && env.WallRight));
                if (slide)
                {
                    limit = 160f + (20f - 160f) * (wallSlideTime / 1.2f);
                    wallSlideTime = Down(wallSlideTime, dt);
                }
                float gravity = Math.Abs(Y) < 40f && input.JumpHeld ? 450f : 900f;
                Y = Approach(Y, -limit, gravity * dt);
            }
            if (env.Ceiling && Y > 0) { Y = 0; variableJump = 0; }
            if (variableJump > 0)
            {
                if (input.JumpHeld && !env.Ceiling) Y = Math.Max(Y, variableSpeed);
                else variableJump = 0;
            }
            if (jumpBuffer > 0)
            {
                if (coyote > 0)
                {
                    X += 40f * input.X;
                    StartJump();
                }
                else if (EnableWallJump && (env.WallLeft || env.WallRight))
                {
                    int away = env.WallRight ? -1 : 1;
                    X = away * 130f; Facing = away;
                    if (input.X != 0) { forcedX = away; forcedTime = .16f; }
                    StartJump(); WallJumped = true;
                }
            }
            Finish(dt);
        }

        private void StartJump()
        {
            Y = variableSpeed = JumpSpeed; variableJump = .2f;
            jumpBuffer = coyote = 0; wallSlideTime = 1.2f; Jumped = true;
        }

        private void Finish(float dt)
        {
            jumpBuffer = Down(jumpBuffer, dt); dashBuffer = Down(dashBuffer, dt);
            variableJump = Down(variableJump, dt); hasSimulated = true;
        }

        private static float Down(float value, float dt) => Math.Max(0, value - dt);
        private static float Approach(float value, float target, float amount) =>
            value < target ? Math.Min(value + amount, target) : Math.Max(value - amount, target);
    }
}
