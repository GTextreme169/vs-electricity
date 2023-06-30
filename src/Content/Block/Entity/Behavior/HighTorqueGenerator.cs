using Vintagestory.API.Common;

namespace Electricity.Content.Block.Entity.Behavior {
    public sealed class HighTorqueGenerator : Generator  {
        
        protected override float ProduceFactor => 1000.0f;

        public HighTorqueGenerator(BlockEntity blockEntity) : base(blockEntity) { }
    }
}
