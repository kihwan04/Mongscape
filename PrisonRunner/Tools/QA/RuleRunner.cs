using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
class RuleRunner
{
 static int Main(string[] args)
 {
  int passed=0,failed=0;
  var assembly=Assembly.LoadFrom("Muhanok.EditModeTests.dll");
  foreach(string name in args.Length>0?args:new[]{"ExpansionRulesTests","WorkoutEstimateTests","RunRulesTests","BodyTrackingAndExhibitionTests","IdentityServerConfigTests","MuhanokTestIntegrationTests"})
  {
   var type=assembly.GetType("Muhanok.Tests."+name);var instance=Activator.CreateInstance(type);
   foreach(var method in type.GetMethods())
   {
    if(method.Name=="DailyRankingSortsAndPersistsPerDay"||method.Name=="PlayerStorePersistsProfileWalletRecordsAndStats"||method.Name=="LegacyCoinsAndRankingsMigrateOnceWithoutChangingOriginals")continue;
    var cases=method.GetCustomAttributes(typeof(TestCaseAttribute),true).Cast<TestCaseAttribute>().ToArray();
    if(cases.Length==0 && !method.IsDefined(typeof(TestAttribute),true))continue;
    var inputs=cases.Length>0?cases.Select(c=>c.Arguments):new[]{new object[0]};
    foreach(var input in inputs)
    {
     try { method.Invoke(instance,input);passed++;Console.WriteLine("PASS "+name+"."+method.Name+"("+string.Join(",",input)+")"); }
     catch(Exception e){failed++;Console.WriteLine("FAIL "+name+"."+method.Name+": "+(e.InnerException??e));}
    }
   }
  }
  Console.WriteLine("PURE_RULE_TESTS: "+passed+" passed, "+failed+" failed");return failed==0?0:1;
 }
}
