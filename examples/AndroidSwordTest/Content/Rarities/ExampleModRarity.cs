using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace AndroidSwordTest.Content.Rarities
{
	public class ExampleModRarity : ModRarity
	{
		public override Color RarityColor => new Color(200, 215, 230);

		public override int GetPrefixedRarity(int offset, float valueMult) {
			if (offset > 0) {
				return ModContent.RarityType<ExampleHigherTierModRarity>();
			}

			return Type;
		}
	}
}
