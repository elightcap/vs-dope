using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using VsDope.Systems;

namespace VsDope.Client;

/// <summary>Temporary tint on the vanilla sclera texture region; never edits skinConfig.</summary>
public sealed class StonedEyesBehavior : EntityBehavior
{
    private ICoreClientAPI? capi;
    private EntityBehaviorTexturedClothing? clothing;
    private LoadedTexture? tint;
    private bool attached;
    public StonedEyesBehavior(Entity entity) : base(entity) { }
    public override string PropertyName() => "vsdope-stonedeyes";
    public override void OnEntitySpawn() { base.OnEntitySpawn(); Attach(); }
    public override void OnEntityLoaded() { base.OnEntityLoaded(); Attach(); }

    private void Attach()
    {
        if (attached || entity.Api is not ICoreClientAPI api) return;
        capi = api;
        clothing = entity.GetBehavior<EntityBehaviorTexturedClothing>();
        if (clothing == null || entity.GetBehavior<EntityBehaviorExtraSkinnable>() == null) return;
        clothing.OnReloadSkin += RenderEyes;
        entity.WatchedAttributes.RegisterModifiedListener(StonedSystem.ExpiryKey, Refresh);
        attached = true;
        Refresh();
    }

    private void Refresh()
    {
        if (clothing == null) return;
        clothing.doReloadShapeAndSkin = true;
        entity.MarkShapeModified();
    }

    private void RenderEyes(LoadedTexture atlas, TextureAtlasPosition position, int subId)
    {
        if (capi == null || !entity.Alive || entity.WatchedAttributes.GetDouble(StonedSystem.ExpiryKey) <= capi.World.Calendar.TotalHours) return;
        var skin = entity.GetBehavior<EntityBehaviorExtraSkinnable>();
        // Only the standard Seraph atlas layout is supported; custom player models keep their own eyes.
        if (skin?.mainTextureCode != "seraph" || !skin.AvailableSkinPartsByCode.TryGetValue("eyecolor", out var eyes)
            || eyes.TextureRenderTo == null || eyes.TextureRenderTo.X != 57 || eyes.TextureRenderTo.Y != 0) return;
        tint ??= new LoadedTexture(capi);
        capi.Render.GetOrLoadTexture(new AssetLocation("vs-dope:textures/entity/stoned-eyes.png"), ref tint);
        capi.EntityTextureAtlas.RenderTextureIntoAtlas(position.atlasTextureId, tint, 0, 0, 2, 3,
            position.x1 * capi.EntityTextureAtlas.Size.Width + eyes.TextureRenderTo.X,
            position.y1 * capi.EntityTextureAtlas.Size.Height + eyes.TextureRenderTo.Y, .005f);
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        if (clothing != null) clothing.OnReloadSkin -= RenderEyes;
        entity.WatchedAttributes.UnregisterListener(Refresh);
        // GetOrLoadTexture is cache-owned; do not dispose the shared texture here.
        tint = null;
        base.OnEntityDespawn(despawn);
    }
}
