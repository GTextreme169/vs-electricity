using Electricity.Interface;
using Vintagestory.API.Common;

namespace Electricity.Content.Block.Entity.Behavior {
    public sealed class HighTorqueMotor : Motor {
        public override ConsumptionRange ConsumptionRange => new ConsumptionRange(50, 1000);

        public HighTorqueMotor(BlockEntity blockEntity) : base(blockEntity) { }
    }
}
