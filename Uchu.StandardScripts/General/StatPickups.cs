using System.Threading.Tasks;
using Uchu.World;
using Uchu.World.Scripting.Native;

namespace Uchu.StandardScripts.General
{
    public class StatPickups : NativeScript
    {
        public override Task LoadAsync()
        {
            Listen(Zone.OnPlayerLoad, player =>
            {
                Listen(player.OnLootPickup, async lot =>
                {
                    var stats = player.GetComponent<Stats>();

                    var health = stats.Health;
                    var armor = stats.Armor;
                    var imagination = stats.Imagination;

                    switch (lot)
                    {
                        case Lot.Imagination:
                            imagination += 1;
                            break;
                        case Lot.TwoImagination:
                            imagination += 2;
                            break;
                        case Lot.ThreeImagination:
                            imagination += 3;
                            break;
                        case Lot.FiveImagination:
                            imagination += 5;
                            break;
                        case Lot.TenImagination:
                            imagination += 10;
                            break;
                        case Lot.Health:
                            health += 1;
                            break;
                        case Lot.TwoHealth:
                            health += 2;
                            break;
                        case Lot.ThreeHealth:
                            health += 3;
                            break;
                        case Lot.FiveHealth:
                            health += 5;
                            break;
                        case Lot.TenHealth:
                            health += 10;
                            break;
                        case Lot.Armor:
                            armor += 1;
                            break;
                        case Lot.TwoArmor:
                            armor += 2;
                            break;
                        case Lot.ThreeArmor:
                            armor += 3;
                            break;
                        case Lot.FiveArmor:
                            armor += 5;
                            break;
                        case Lot.TenArmor:
                            armor += 10;
                            break;
                        default:
                            return;
                    }

                    await stats.SetHealthAsync(health);
                    await stats.SetArmorAsync(armor);
                    await stats.SetImaginationAsync(imagination);
                });

                return Task.CompletedTask;
            });

            return Task.CompletedTask;
        }
    }
}