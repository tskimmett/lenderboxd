namespace Lenderboxd.Unit;

[TestClass]
public class ArmyTests
{
    [Ignore]
    [TestMethod]
    [DataRow(UnitType.Infantry)]
    [DataRow(UnitType.Cavalry)]
    [DataRow(UnitType.Archers)]
    [DataRow(UnitType.Maji)]
    public void Battle_OneVsOne_EqualDamage(UnitType unitType)
    {
        var a = new ArmyUnit(unitType, 1);
        var b = new ArmyUnit(unitType, 2);

        // a.Attack(b);

        Assert.AreEqual<Percent>(50, a.Health);
        Assert.AreEqual<Percent>(50, b.Health);
    }
}