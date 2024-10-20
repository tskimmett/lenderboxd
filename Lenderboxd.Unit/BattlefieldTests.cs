namespace Lenderboxd.Unit;

[TestClass]
public class BattlefieldTests : BaseGrainTests
{
	[TestMethod]
	public async Task Battle_Army_Vs_NoArmy()
	{
		var attacker = GetGrain<IPlayerGrain>(Guid.NewGuid());
		await Task.WhenAll(
			attacker.RecruitArmyUnit(UnitType.Archers),
			attacker.RecruitArmyUnit(UnitType.Archers),
			attacker.RecruitArmyUnit(UnitType.Archers)
		);

		var defender = GetGrain<IPlayerGrain>(Guid.NewGuid());

		var battlefield = GetGrain<IBattlefieldGrain>(0);

		await battlefield.Battle(attacker, defender);
	}
}