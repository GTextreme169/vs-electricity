

using System.Collections.Generic;
using Electricity.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Electricity.Content.Block
{
  public class ElectricFirepit : Vintagestory.API.Common.Block
  {
    public bool IsExtinct;
    private AdvancedParticleProperties[] ringParticles;
    private Vec3f[] basePos;
    private WorldInteraction[] interactions;

    public override void OnLoaded(ICoreAPI api)
    {
      base.OnLoaded(api);
      this.IsExtinct = this.LastCodePart() != "lit";
      if (!this.IsExtinct && api.Side == EnumAppSide.Client && this.ParticleProperties != null)
      {
        this.ringParticles = new AdvancedParticleProperties[this.ParticleProperties.Length * 4];
        this.basePos = new Vec3f[this.ringParticles.Length];
        Cuboidf[] cuboidfArray = new Cuboidf[4]
        {
          new Cuboidf(0.125f, 0.0f, 0.125f, 5f / 16f, 0.5f, 0.875f),
          new Cuboidf(0.7125f, 0.0f, 0.125f, 0.875f, 0.5f, 0.875f),
          new Cuboidf(0.125f, 0.0f, 0.125f, 0.875f, 0.5f, 5f / 16f),
          new Cuboidf(0.125f, 0.0f, 0.7125f, 0.875f, 0.5f, 0.875f)
        };
        for (int index1 = 0; index1 < this.ParticleProperties.Length; ++index1)
        {
          for (int index2 = 0; index2 < 4; ++index2)
          {
            AdvancedParticleProperties particleProperties = this.ParticleProperties[index1].Clone();
            Cuboidf cuboidf = cuboidfArray[index2];
            this.basePos[index1 * 4 + index2] = new Vec3f(0.0f, 0.0f, 0.0f);
            particleProperties.PosOffset[0].avg = cuboidf.MidX;
            particleProperties.PosOffset[0].var = cuboidf.Width / 2f;
            particleProperties.PosOffset[1].avg = 0.1f;
            particleProperties.PosOffset[1].var = 0.05f;
            particleProperties.PosOffset[2].avg = cuboidf.MidZ;
            particleProperties.PosOffset[2].var = cuboidf.Length / 2f;
            particleProperties.Quantity.avg /= 4f;
            particleProperties.Quantity.var /= 4f;
            this.ringParticles[index1 * 4 + index2] = particleProperties;
          }
        }
      }

      this.interactions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api,
        "firepitInteractions-5", (CreateCachableObjectDelegate<WorldInteraction[]>)(() =>
        {
          return new WorldInteraction[1]
          {
            new WorldInteraction()
            {
              ActionLangCode = "blockhelp-firepit-open",
              MouseButton = EnumMouseButton.Right,
              ShouldApply = (InteractionMatcherDelegate)((wi, blockSelection, entitySelection) => true)
            },
          };
        }));
    }

    public override void OnEntityInside(IWorldAccessor world, Vintagestory.API.Common.Entities.Entity entity, BlockPos pos)
    {
      if (world.Rand.NextDouble() < 0.05)
      {
        Block.Entity.ElectricFirepit blockEntity = this.GetBlockEntity<Block.Entity.ElectricFirepit>(pos);
        // ISSUE: explicit non-virtual call
        if ((blockEntity != null ? (blockEntity.IsBurning? 1 : 0) : 0) != 0)
          entity.ReceiveDamage(new DamageSource()
          {
            Source = EnumDamageSource.Block,
            SourceBlock = (Vintagestory.API.Common.Block)this,
            Type = EnumDamageType.Fire,
            SourcePos = pos.ToVec3d()
          }, 0.5f);
      }

      base.OnEntityInside(world, entity, pos);
    }

    public override bool ShouldReceiveClientParticleTicks(
      IWorldAccessor world,
      IPlayer player,
      BlockPos pos,
      out bool isWindAffected)
    {
      int num = base.ShouldReceiveClientParticleTicks(world, player, pos, out bool _) ? 1 : 0;
      isWindAffected = true;
      return num != 0;
    }

    public override void OnAsyncClientParticleTick(
      IAsyncParticleManager manager,
      BlockPos pos,
      float windAffectednessAtPos,
      float secondsTicking)
    {
      if (this.IsExtinct)
        base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
      else if (manager.BlockAccess.GetBlockEntity(pos) is Block.Entity.ElectricFirepit blockEntity &&
               blockEntity.CurrentModel == EnumFirepitModel.Wide)
      {
        for (int index = 0; index < this.ringParticles.Length; ++index)
        {
          AdvancedParticleProperties ringParticle = this.ringParticles[index];
          ringParticle.WindAffectednesAtPos = windAffectednessAtPos;
          ringParticle.basePos.X = (double)pos.X + (double)this.basePos[index].X;
          ringParticle.basePos.Y = (double)pos.Y + (double)this.basePos[index].Y;
          ringParticle.basePos.Z = (double)pos.Z + (double)this.basePos[index].Z;
          manager.Spawn((IParticlePropertiesProvider)ringParticle);
        }
      }
      else
        base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
    }

    public override bool OnBlockInteractStart(
      IWorldAccessor world,
      IPlayer byPlayer,
      BlockSelection blockSel)
    {
      if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
        return false;
      ItemStack itemstack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is Block.Entity.ElectricFirepit blockEntity)
        {
          if (blockEntity != null && itemstack != null && byPlayer.Entity.Controls.ShiftKey)
          {
            if (itemstack.Collectible.CombustibleProps != null &&
                itemstack.Collectible.CombustibleProps.MeltingPoint > 0)
            {
              ItemStackMoveOperation op = new ItemStackMoveOperation(world, EnumMouseButton.Button1, (EnumModifierKey)0,
                EnumMergePriority.DirectMerge, 1);
              byPlayer.InventoryManager.ActiveHotbarSlot?.TryPutInto(blockEntity.inputSlot, ref op);
              if (op.MovedQuantity > 0)
              {
                if (byPlayer is IClientPlayer clientPlayer)
                  clientPlayer.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                return true;
              }
            }

            if (itemstack.Collectible.CombustibleProps != null &&
                itemstack.Collectible.CombustibleProps.BurnTemperature > 0)
            {
              ItemStackMoveOperation op = new ItemStackMoveOperation(world, EnumMouseButton.Button1, (EnumModifierKey)0,
                EnumMergePriority.DirectMerge, 1);
              if (op.MovedQuantity > 0)
              {
                if (byPlayer is IClientPlayer clientPlayer)
                  clientPlayer.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                JsonObject itemAttributes = itemstack.ItemAttributes;
                AssetLocation assetLocation =
                  (itemAttributes != null ? (itemAttributes["placeSound"].Exists ? 1 : 0) : 0) != 0
                    ? AssetLocation.Create(itemstack.ItemAttributes["placeSound"].AsString(),
                      itemstack.Collectible.Code.Domain)
                    : (AssetLocation)null;
                if (assetLocation != (AssetLocation)null)
                  this.api.World.PlaySoundAt(assetLocation.WithPathPrefixOnce("sounds/"), (double)blockSel.Position.X,
                    (double)blockSel.Position.Y, (double)blockSel.Position.Z, byPlayer,
                    (float)(0.8799999952316284 + this.api.World.Rand.NextDouble() * 0.23999999463558197), 16f);
                return true;
              }
            }
          }

          if (itemstack != null)
          {
            // ISSUE: explicit non-virtual call
            bool? nullable = itemstack.Collectible.Attributes?.IsTrue("mealContainer");
            bool flag = true;
            if (nullable.GetValueOrDefault() == flag & nullable.HasValue)
            {
              ItemSlot potslot = (ItemSlot)null;
              if (blockEntity?.inputStack?.Collectible is BlockCookedContainer)
                potslot = blockEntity.inputSlot;
              if (blockEntity?.outputStack?.Collectible is BlockCookedContainer)
                potslot = blockEntity.outputSlot;
              if (potslot != null)
              {
                BlockCookedContainer collectible = potslot.Itemstack.Collectible as BlockCookedContainer;
                ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (byPlayer.InventoryManager.ActiveHotbarSlot.StackSize > 1)
                {
                  ItemSlot bowlSlot = (ItemSlot)new DummySlot(activeHotbarSlot.TakeOut(1));
                  byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                  collectible.ServeIntoStack(bowlSlot, potslot, world);
                  if (!byPlayer.InventoryManager.TryGiveItemstack(bowlSlot.Itemstack, true))
                    world.SpawnItemEntity(bowlSlot.Itemstack, byPlayer.Entity.ServerPos.XYZ);
                }
                else
                  collectible.ServeIntoStack(activeHotbarSlot, potslot, world);
              }
              else if (!blockEntity.inputSlot.Empty ||
                       byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(this.api.World, blockEntity.inputSlot) ==
                       0)
                blockEntity.OnPlayerRightClick(byPlayer, blockSel);

              return true;
            }
          }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
      }

      if (itemstack == null)
        return false;
      if (byPlayer != null && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
      return true;
    }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos) {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (world.BlockAccessor.GetBlockEntity(pos) is Entity.Cable entity) {
                var blockFacing = BlockFacing.FromVector(neibpos.X - pos.X, neibpos.Y - pos.Y, neibpos.Z - pos.Z);
                var selectedFacing = FacingHelper.FromFace(blockFacing);

                if ((entity.Connection & ~ selectedFacing) == Facing.None) {
                    world.BlockAccessor.BreakBlock(pos, null);

                    return;
                }
                
                var selectedConnection = entity.Connection & selectedFacing;

                if (selectedConnection != Facing.None) {
                    var stackSize = FacingHelper.Count(selectedConnection);

                    if (stackSize > 0) {
                      entity.Connection &= ~selectedConnection;
                    }
                }
            }
        }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSelection, ItemStack byItemStack) {
      var selection = new Selection(blockSelection);
      var facing = FacingHelper.From(selection.Face, selection.Direction);

      /* update existing cable */
      {
        if (world.BlockAccessor.GetBlockEntity(blockSelection.Position) is Entity.Cable entity) {
          if ((entity.Connection & facing) != 0) {
            return false;
          }

          entity.Connection |= facing;

          return true;
        }
      }

      if (base.DoPlaceBlock(world, byPlayer, blockSelection, byItemStack)) {
        if (world.BlockAccessor.GetBlockEntity(blockSelection.Position) is Entity.Cable entity) {
          entity.Connection = facing;
        }

        return true;
      }

      return false;
    }
    
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(
      IWorldAccessor world,
      BlockSelection selection,
      IPlayer forPlayer)
    {
      return this.interactions.Append<WorldInteraction>(
        base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }
  }
}