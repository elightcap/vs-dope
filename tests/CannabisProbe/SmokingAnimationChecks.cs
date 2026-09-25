using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VsDope.Items;

namespace VsDope.Tests;

internal static class SmokingAnimationChecks
{
    public static void Verify(ICoreServerAPI api, Action<bool, string> check)
    {
        foreach (string model in new[] { "seraph-faceless", "seraph" })
        {
            string path = $"game:shapes/entity/humanoid/{model}.json";
            // Load the actual patched asset and resolve it just as a client does.
            var shape = api.Assets.Get(new AssetLocation(path)).ToObject<Shape>();
            shape.InitForAnimations(api.Logger, path);

            foreach (string code in new[] { JointItem.AnimationCode, JointItem.AnimationCode + "-fp",
                BongItem.AnimationCode, BongItem.AnimationCode + "-fp" })
            {
                var animation = shape.Animations.Single(a => a.Code == code);
                string context = $"{model}/{code}";
                check(animation.KeyFrames.All(frame =>
                    new[] { "UpperArmR", "LowerArmR" }.All(name =>
                        frame.Elements.TryGetValue(name, out var element) && element.ForElement?.Name == name)),
                    context + " keyframes resolve to the player arms");

                // Parsing JSON or mocking StartAnimation misses incomplete nullable vectors.
                // This is the native frame generator called by ClientAnimator on first use.
                api.Logger.Notification("CANNABIS generating animation: " + context);
                animation.GenerateAllFrames(shape.Elements, shape.JointsById);
                check(animation.QuantityFrames == 150 &&
                    animation.PrevNextKeyFrameByFrame.Length == 150 &&
                    animation.PrevNextKeyFrameByFrame.All(pair =>
                        pair.Length == 2 && pair.All(frame => frame.RootElementTransforms.Count > 0)),
                    context + " generates all 150 animation frames without throwing");
            }
        }
    }
}
