using System;
using Vintagestory.API.Common;

namespace miningtools
{
    public class MiningTools : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("ItemAOETool", typeof(ItemAOETool));
        }
    }
}
