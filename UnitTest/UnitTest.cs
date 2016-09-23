using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnitTest
{
    [TestClass]
    public class UnitTest
    {
        Dictionary<string, List<string>> simplewords = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> numbers     = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> exceptions  = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> consonant   = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> possesive   = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> others      = new Dictionary<string, List<string>>();
        
        [TestInitialize]
        public void setUp()
        {
            var test_list = new TupleList<string, Dictionary<string, List<string>>>(){
                {"simplewords", simplewords},
                {"numbers"    , numbers    },
                {"exceptions" , exceptions },
                {"consonantharmony", consonant},
                {"possesive", possesive},
                {"others", others}
            };
            foreach (var eachTest in test_list)
	        {
		        foreach (var line in File.ReadLines("tests\\" + eachTest.Item1))
                {
                    if (line.Trim().Length == 0) continue;
                    string[] split = line.Trim().Split('=');
                    string name = split[0].Trim();
                    List<string> suffixes = new List<string>(split[1].Trim().Substring(1,split[1].Length - 3).Split(','));
                    eachTest.Item2.Add(name, suffixes);
                }
	        }
        }
        [TestMethod]
        public void simpleWordsTest()
        {
            baseTest(simplewords);
        }
        [TestMethod]
        public void numberTest()
        {
            baseTest(numbers);
        }
        [TestMethod]
        public void exceptionTest()
        {
            baseTest(exceptions);
        }
        [TestMethod]
        public void consonantHarmonyTest()
        {
            baseTest(consonant);
        }
        [TestMethod]
        public void possesiveTest()
        {
            baseTest(possesive);
        }
        [TestMethod]
        public void othersTest()
        {
            baseTest(others);
        }
        private void baseTest(Dictionary<string, List<string>> testList)
        {
            var suffixList = TurkSufFixer.SufFixer.suffixes;
            var ekle = new TurkSufFixer.SufFixer();
            foreach (var item in testList)
            {
                var name = item.Key;
                var correctsf = item.Value;
                foreach (var eachSuff in correctsf.Zip(suffixList, (a,b) => new Tuple<string,string>(a,b)))
                {
                    var rtnSuff = ekle.getSuffix(name, eachSuff.Item2);
                    Assert.AreEqual(rtnSuff, eachSuff.Item1, string.Format("{0} için {1} beklenirken {2} geldi.", name.Trim(), eachSuff.Item1, rtnSuff));
                }

            }
        }
        
    }
    class TupleList<T1, T2> : List<Tuple<T1, T2>>
    {
        public void Add(T1 item, T2 item2)
        {
            Add(new Tuple<T1, T2>(item, item2));
        }
    }
}
