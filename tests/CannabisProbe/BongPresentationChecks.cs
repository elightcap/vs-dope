using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VsDope.Items;

namespace VsDope.Tests;

internal static class BongPresentationChecks
{
    private static readonly Vec4f Grip = new(6.5f / 16, 7f / 16, 8f / 16, 1);
    private static readonly Vec4f Rim = new(6.5f / 16, 14.15f / 16, 8f / 16, 1);
    private static readonly Vec4f Base = new(6.5f / 16, .3f / 16, 8f / 16, 1);

    public static void Verify(ICoreServerAPI api, Action<bool, string> check)
    {
        var metadata = api.Assets.Get(new AssetLocation("game:entities/humanoid/player.json"))
            .ToObject<JObject>()["client"]!["animations"]!.ToObject<AnimationMetaData[]>()!;
        foreach (string variant in new[] { "empty", "loaded" })
        {
            var item = api.World.GetItem(new AssetLocation("vs-dope:bong-" + variant))!;
            var tf = item.GuiTransform;
            // InventoryItemRenderer uses downward screen Y and no automatic X flip for items.
            var gui = new Matrixf().Identity().Scale(tf.ScaleXYZ.X, tf.ScaleXYZ.Y, tf.ScaleXYZ.Z)
                .RotateXDeg(tf.Rotation.X).RotateYDeg(tf.Rotation.Y).RotateZDeg(tf.Rotation.Z);
            check(gui.TransformVector(Rim).Y < gui.TransformVector(Base).Y,
                variant + " bong GUI mouthpiece is above its base in native screen coordinates");
            check(item.GroundTransform.Rotation.X == 0, variant + " bong ground transform stands upright");

            foreach (string model in new[] { "seraph-faceless", "seraph" })
            foreach (string code in new[] { BongItem.AnimationCode, BongItem.AnimationCode + "-fp" })
            {
                string context = variant + "/" + model + "/" + code;
                var shape = api.Assets.Get(new AssetLocation($"game:shapes/entity/humanoid/{model}.json")).ToObject<Shape>();
                shape.InitForAnimations(api.Logger, model);
                var animator = new ClientAnimator(() => 1, shape.Animations, shape.Elements, shape.JointsById);
                string idleCode = code.EndsWith("-fp") ? "helditemready-fp" : "helditemready";
                var idle = metadata.Single(a => a.Code == idleCode).Clone().Init();
                // Vanilla FP held-idle has zero weight outside the arms and needs the body idle.
                string bodyCode = code.EndsWith("-fp") ? "idle-fp" : "idle";
                var body = metadata.Single(a => a.Code == bodyCode).Clone().Init();
                var active = new Dictionary<string, AnimationMetaData> { [body.Animation] = body, [idle.Animation] = idle };
                for (int i = 0; i < 60; i++) animator.OnFrame(active, 1f / 60);
                var hand = animator.GetAttachmentPointPose("RightHand");
                var held = HeldMatrix(item.TpHandTransform, hand);
                check(Distance(held.TransformVector(Grip), new Matrixf().Set(hand.AnimModelMatrix)
                    .TransformVector(new Vec4f(0, 0, 0, 1))) < .001,
                    context + " neck grip coincides with the actual animated hand attachment");
                check(held.TransformVector(Rim).Y > held.TransformVector(Base).Y + .45,
                    context + " idle bong stands upright in the hand");
                check(BowlFacesForward(held, animator), context + " idle bowl and downstem face away from the player");

                var use = metadata.Single(a => a.Code == code).Clone().Init();
                // PlayerAnimationManager.StartHeldUseAnim stops the held idle/ready animation.
                active.Remove(idle.Animation);
                active[use.Animation] = use;
                for (int i = 0; i < 120; i++) animator.OnFrame(active, 1f / 60);
                held = HeldMatrix(item.TpHandTransform, hand);
                // Native Seraph mouth geometry: x=-.1..0, y=.6...9, z=2..3 relative to Head.
                var mouth = new Matrixf().Set(animator.GetPosebyName("Head").AnimModelMatrix)
                    .TransformVector(new Vec4f(-.02f, .75f / 16, 2.5f / 16, 1));
                double distance = Distance(held.TransformVector(Rim), mouth);
                api.Logger.Notification($"CANNABIS pose {context}: rim-to-mouth {distance:F4} blocks");
                check(distance < .04, context + " blended smoking pose brings the mouthpiece to the face");
                check(BowlFacesForward(held, animator), context + " smoking bowl and downstem face away from the player");
                for (int i = 0; i < 210; i++) animator.OnFrame(active, 1f / 60);
                check(HeldMatrix(item.TpHandTransform, hand).TransformVector(Rim).Y < mouth.Y - .3,
                    context + " lowers the bong before the completion exhale");
            }
        }
    }

    private static bool BowlFacesForward(Matrixf held, ClientAnimator animator)
    {
        // Bowl centre x=11.2 projects out from the neck axis x=6.5. Compare this
        // model-space direction with the native Seraph's negative-X face direction.
        var bowl = held.TransformVector(new Vec4f((11.2f - 6.5f) / 16, 0, 0, 0));
        var forward = new Matrixf().Set(animator.GetPosebyName("Head").AnimModelMatrix)
            .TransformVector(new Vec4f(-1, 0, 0, 0));
        double dot = bowl.X * forward.X + bowl.Y * forward.Y + bowl.Z * forward.Z;
        double lengths = Math.Sqrt((bowl.X * bowl.X + bowl.Y * bowl.Y + bowl.Z * bowl.Z) *
            (forward.X * forward.X + forward.Y * forward.Y + forward.Z * forward.Z));
        return dot > .5 * lengths;
    }

    // Exact 1.22.7 EntityShapeRenderer.RenderItem transform order. Both current camera modes
    // use HandTp; fpHandTransform is kept aligned for compatible renderers.
    private static Matrixf HeldMatrix(ModelTransform tf, AttachmentPointAndPose hand)
    {
        var o = tf.Origin;
        var t = tf.Translation;
        var r = tf.Rotation;
        var ap = hand.AttachPoint;
        return new Matrixf().Set(hand.AnimModelMatrix).Translate(o.X, o.Y, o.Z)
            .Scale(tf.ScaleXYZ.X, tf.ScaleXYZ.Y, tf.ScaleXYZ.Z)
            .Translate(ap.PosX / 16 + t.X, ap.PosY / 16 + t.Y, ap.PosZ / 16 + t.Z)
            .Rotate((float)(ap.RotationX + r.X) * GameMath.DEG2RAD, (float)(ap.RotationY + r.Y) * GameMath.DEG2RAD,
                (float)(ap.RotationZ + r.Z) * GameMath.DEG2RAD).Translate(-o.X, -o.Y, -o.Z);
    }

    private static double Distance(Vec4f a, Vec4f b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
}
