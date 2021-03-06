﻿using System;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using DirectX12GameEngine.Core;

namespace DirectX12GameEngine.Physics
{
    public class RigidBodyComponent : PhysicsComponent
    {
        private bool isKinematic;
        private float mass = 1.0f;

        internal BodyHandle Handle { get; private protected set; }

        public override ColliderShape? ColliderShape
        {
            set
            {
                base.ColliderShape = value;

                if (Simulation != null)
                {
                    Simulation.InternalSimulation.Bodies.SetShape(Handle, value?.ShapeIndex ?? default);
                }
            }
        }

        public bool IsKinematic
        {
            get => isKinematic;
            set
            {
                isKinematic = value;

                if (Simulation != null && isKinematic)
                {
                    Simulation.InternalSimulation.Bodies.GetBodyReference(Handle).BecomeKinematic();
                }
            }
        }

        public float Mass
        {
            get
            {
                return mass;
            }
            set
            {
                if (value < 0.0f)
                {
                    throw new InvalidOperationException("The mass of a RigidBody cannot be negative.");
                }

                mass = value;

                if (Simulation != null)
                {
                    ref BodyInertia inertia = ref Simulation.InternalSimulation.Bodies.GetBodyReference(Handle).LocalInertia;

                    if (ColliderShape is null)
                    {
                        inertia.InverseMass = 1.0f / mass;
                    }
                    else
                    {
                        inertia = ColliderShape.ComputeIntertia(mass);
                    }
                }
            }
        }

        public override Matrix4x4 PhysicsWorldTransform
        {
            get
            {
                if (Simulation is null) return Matrix4x4.Identity;

                ref RigidPose pose = ref Simulation.InternalSimulation.Bodies.GetBodyReference(Handle).Pose;

                return Matrix4x4.CreateFromQuaternion(pose.Orientation) * Matrix4x4.CreateTranslation(pose.Position);
            }
            set
            {
                if (Simulation != null)
                {
                    ref RigidPose pose = ref Simulation.InternalSimulation.Bodies.GetBodyReference(Handle).Pose;

                    (_, pose.Orientation, pose.Position) = value;
                }
            }
        }

        protected override void OnAttach()
        {
            base.OnAttach();

            Matrix4x4.Decompose(Entity!.Transform.WorldMatrix, out _, out Quaternion rotation, out Vector3 translation);
            RigidPose pose = new RigidPose(translation, rotation);

            CollidableDescription collidable = new CollidableDescription(ColliderShape?.ShapeIndex ?? default, 0.1f);
            BodyActivityDescription activity = new BodyActivityDescription(0.01f);

            BodyDescription description = IsKinematic
                ? BodyDescription.CreateKinematic(pose, collidable, activity)
                : BodyDescription.CreateDynamic(pose, ColliderShape?.ComputeIntertia(mass) ?? default, collidable, activity);

            Handle = Simulation!.InternalSimulation.Bodies.Add(description);
            Simulation.RigidBodies.GetOrAddValueRef(Handle.Value) = this;
        }

        protected override void OnDetach()
        {
            base.OnDetach();

            Simulation!.InternalSimulation.Bodies.Remove(Handle);
            Simulation.RigidBodies.Remove(Handle.Value);
        }
    }
}
