using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsDope.Entities;

/// <summary>
/// A drug addict's server-side inventory: rusty gears, scavenged junk it can pay with, and the
/// drugs it bought. Plain stack list (no slots, no client sync); the trade window gets a snapshot
/// by packet. Persisted in the entity's server-only <see cref="Entity.Attributes"/> tree.
/// </summary>
public class AddictPockets
{
    // ---- starting stock (balance) -----------------------------------------
    public const int MinStartingGears = 2;
    public const int MaxStartingGears = 18;
    public const int MinJunkKinds = 2;
    public const int MaxJunkKinds = 4;
    public const double StartingDrugChance = 0.3;

    // Liquid drugs the addict buys are poured into these (vanilla fired jug, 3 L).
    public static readonly AssetLocation LiquidJugCode = new("game:jug-blue-fired");
    public static readonly AssetLocation GearCode = new("game:gear-rusty");

    // Scavenged junk: vanilla code, gear-equivalent value per item, quantity range at spawn.
    // Every code was checked against the vanilla item lang keys (item-<code>) for 1.22.7.
    // Value 0 = worthless filler: it sits in the pockets and drops on death but never pays.
    private static readonly (string Code, int GearValue, int Min, int Max)[] Junk =
    {
        ("game:flint", 1, 1, 4),
        ("game:bone", 1, 1, 3),
        ("game:feather", 1, 1, 5),
        ("game:cattailtops", 1, 2, 6),
        ("game:flaxtwine", 1, 1, 3),
        ("game:candle", 2, 1, 3),
        ("game:nugget-nativecopper", 2, 1, 4),
        ("game:beeswax", 2, 1, 2),
        ("game:rope", 3, 1, 1),
        ("game:cloth-plain", 3, 1, 2),
        ("game:bread-spelt-perfect", 3, 1, 1),
        ("game:metalnailsandstrips-copper", 4, 1, 2),
        ("game:redmeat-cured", 4, 1, 2),
        ("game:bandage-clean", 4, 1, 2),
        ("game:rot", 0, 1, 4),
    };

    // A lucky addict still has a little of its own stash (never used as payment).
    private static readonly (string Code, int Min, int Max)[] StartingDrugs =
    {
        ("vs-dope:opium", 1, 2),
        ("vs-dope:morphine", 1, 1),
        ("vs-dope:coca-vitae", 1, 1),
    };

    private const string KeyCount = "count";
    private const string KeySlotPrefix = "s";

    private readonly List<ItemStack> stacks = new();

    public IReadOnlyList<ItemStack> Stacks => stacks;

    // ---- valuation --------------------------------------------------------

    public static bool IsGear(ItemStack? stack)
        => stack?.Collectible?.Code != null && stack.Collectible.Code.Equals(GearCode);

    /// <summary>Gear-equivalent value of one item of this stack when paying (0 = not payment).</summary>
    public static int PaymentValueOf(ItemStack? stack)
    {
        var code = stack?.Collectible?.Code;
        if (code == null) return 0;
        foreach (var junk in Junk)
        {
            if (code.Equals(new AssetLocation(junk.Code))) return junk.GearValue;
        }
        return 0;
    }

    public int GearCount => stacks.Where(IsGear).Sum(s => s.StackSize);

    /// <summary>Total gear-equivalent value of everything except gears that the addict would pay with.</summary>
    public int GoodsValue => stacks.Where(s => !IsGear(s)).Sum(s => PaymentValueOf(s) * s.StackSize);

    public int Wealth => GearCount + GoodsValue;

    // ---- persistence -------------------------------------------------------

    public void Load(ITreeAttribute tree, IWorldAccessor world)
    {
        stacks.Clear();
        int count = tree.GetInt(KeyCount);
        for (int i = 0; i < count; i++)
        {
            var stack = tree.GetItemstack(KeySlotPrefix + i, null);
            if (stack != null && stack.StackSize > 0 && stack.ResolveBlockOrItem(world)) stacks.Add(stack);
        }
    }

    public TreeAttribute ToTree()
    {
        var tree = new TreeAttribute();
        tree.SetInt(KeyCount, stacks.Count);
        for (int i = 0; i < stacks.Count; i++) tree.SetItemstack(KeySlotPrefix + i, stacks[i]);
        return tree;
    }

    // ---- stock -------------------------------------------------------------

    public void RollStartingStock(IWorldAccessor world, Random rng)
    {
        stacks.Clear();

        var gear = world.GetItem(GearCode);
        if (gear != null) Add(world, new ItemStack(gear, rng.Next(MinStartingGears, MaxStartingGears + 1)));

        int kinds = rng.Next(MinJunkKinds, MaxJunkKinds + 1);
        foreach (var junk in Junk.OrderBy(_ => rng.Next()).Take(kinds))
        {
            var item = world.GetItem(new AssetLocation(junk.Code));
            if (item == null)
            {
                world.Logger.Warning("[vs-dope] addict junk item '{0}' not found; skipped", junk.Code);
                continue;
            }
            Add(world, new ItemStack(item, rng.Next(junk.Min, junk.Max + 1)));
        }

        if (rng.NextDouble() < StartingDrugChance)
        {
            var drug = StartingDrugs[rng.Next(StartingDrugs.Length)];
            var item = world.GetItem(new AssetLocation(drug.Code));
            if (item != null) Add(world, new ItemStack(item, rng.Next(drug.Min, drug.Max + 1)));
        }
    }

    // ---- adding ------------------------------------------------------------

    /// <summary>Merge a solid stack into the pockets, topping up equal stacks first.</summary>
    public void Add(IWorldAccessor world, ItemStack stack)
    {
        if (stack?.Collectible == null || stack.StackSize <= 0) return;
        int max = Math.Max(1, stack.Collectible.MaxStackSize);

        foreach (var existing in stacks)
        {
            if (stack.StackSize <= 0) break;
            if (existing.StackSize >= max) continue;
            if (!existing.Equals(world, stack, GlobalConstants.IgnoredStackAttributes)) continue;
            int move = Math.Min(max - existing.StackSize, stack.StackSize);
            existing.StackSize += move;
            stack.StackSize -= move;
        }

        while (stack.StackSize > 0)
        {
            var part = stack.Clone();
            part.StackSize = Math.Min(max, stack.StackSize);
            stacks.Add(part);
            stack.StackSize -= part.StackSize;
        }
    }

    /// <summary>
    /// Store <paramref name="litres"/> of a liquid drug. Liquids only exist inside containers, so
    /// they go into fired jugs (existing jugs of the same liquid are topped up first). Returns the
    /// litres actually stored.
    /// </summary>
    public int AddLiquid(IWorldAccessor world, Item liquid, int litres)
    {
        if (litres <= 0) return 0;
        var props = BlockLiquidContainerBase.GetContainableProps(new ItemStack(liquid));
        if (props == null || props.ItemsPerLitre <= 0) return 0;
        if (world.GetBlock(LiquidJugCode) is not BlockLiquidContainerBase jug) return 0;

        int portionsLeft = (int)Math.Round(litres * props.ItemsPerLitre);
        int stored = 0;

        foreach (var existing in stacks)
        {
            if (portionsLeft <= 0) break;
            if (existing.Block is not BlockLiquidContainerBase container) continue;
            var content = container.GetContent(existing);
            if (content?.Collectible?.Code == null || !content.Collectible.Code.Equals(liquid.Code)) continue;
            int moved = container.TryPutLiquid(existing, new ItemStack(liquid, portionsLeft), portionsLeft / props.ItemsPerLitre);
            portionsLeft -= moved;
            stored += moved;
        }

        // Guard against a zero-capacity jug definition looping forever.
        int safety = 64;
        while (portionsLeft > 0 && safety-- > 0)
        {
            var jugStack = new ItemStack(jug, 1);
            int moved = jug.TryPutLiquid(jugStack, new ItemStack(liquid, portionsLeft), portionsLeft / props.ItemsPerLitre);
            if (moved <= 0) break;
            stacks.Add(jugStack);
            portionsLeft -= moved;
            stored += moved;
        }

        return (int)Math.Round(stored / props.ItemsPerLitre);
    }

    // ---- paying ------------------------------------------------------------

    /// <summary>
    /// Remove <paramref name="price"/> gears' worth from the pockets: gears first, then junk by
    /// gear-equivalent value (may overpay slightly, no change given). Returns the stacks to hand
    /// over; <paramref name="gearsPaid"/> is the gear part. Call only when <see cref="Wealth"/> covers it.
    /// </summary>
    public List<ItemStack> TakePayment(IWorldAccessor world, int price, out int gearsPaid)
    {
        var paid = new List<ItemStack>();
        gearsPaid = TakeFrom(IsGear, Math.Min(price, GearCount), paid, world);
        int remaining = price - gearsPaid;

        while (remaining > 0)
        {
            // Most valuable item that doesn't overshoot, else the cheapest one that covers the rest.
            var candidates = stacks.Where(s => !IsGear(s) && s.StackSize > 0 && PaymentValueOf(s) > 0).ToList();
            if (candidates.Count == 0) break;
            var pick = candidates.Where(s => PaymentValueOf(s) <= remaining).OrderByDescending(PaymentValueOf).FirstOrDefault()
                    ?? candidates.OrderBy(PaymentValueOf).First();

            remaining -= PaymentValueOf(pick);
            TakeFrom(s => ReferenceEquals(s, pick), 1, paid, world);
        }

        return paid;
    }

    private int TakeFrom(System.Func<ItemStack, bool> match, int want, List<ItemStack> into, IWorldAccessor world)
    {
        int taken = 0;
        for (int i = stacks.Count - 1; i >= 0 && taken < want; i--)
        {
            var stack = stacks[i];
            if (!match(stack)) continue;
            int move = Math.Min(stack.StackSize, want - taken);
            var part = stack.Clone();
            part.StackSize = move;
            MergeInto(into, part, world);
            stack.StackSize -= move;
            taken += move;
            if (stack.StackSize <= 0) stacks.RemoveAt(i);
        }
        return taken;
    }

    private static void MergeInto(List<ItemStack> list, ItemStack part, IWorldAccessor world)
    {
        foreach (var existing in list)
        {
            if (existing.StackSize + part.StackSize <= existing.Collectible.MaxStackSize &&
                existing.Equals(world, part, GlobalConstants.IgnoredStackAttributes))
            {
                existing.StackSize += part.StackSize;
                return;
            }
        }
        list.Add(part);
    }

    /// <summary>Everything, emptied out (for dropping on death).</summary>
    public List<ItemStack> TakeAll()
    {
        var all = stacks.ToList();
        stacks.Clear();
        return all;
    }
}
