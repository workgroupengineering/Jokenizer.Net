using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace Jokenizer.Net.Tests {
    using Fixture;
    using Tokens;

    public class EvaluatorTests {

        static EvaluatorTests() {
            ExtensionMethods.ProbeAllAssemblies();
            ExtensionMethods.ProbeAssemblies(typeof(Extensions).Assembly);
        }

        [Fact]
        public void ShouldEvaluateNumber() {
            var v = Evaluator.ToFunc<int>("42");
            Assert.Equal(42, v());
        }

        [Fact]
        public void ShouldEvaluateFloatNumber() {
            var v = Evaluator.ToFunc<float>("42.4242");
            Assert.Equal(42.4242, v(), 1);
        }

        [Fact]
        public void ShouldEvaluateString() {
            var v1 = Evaluator.ToFunc<string>("\"4\\\"2\"");
            Assert.Equal("4\"2", v1());

            var v2 = Evaluator.ToFunc<string>("\"\\a\\b\\f\\n\\r\\t\\v\\0\\\"\\\\\"");
            Assert.Equal("\a\b\f\n\r\t\v\0\"\\", v2());
        }

        [Fact]
        public void ShouldEvaluateInterpolatedString() {
            var v = Evaluator.ToFunc<string>("$\"don't {@0}, 42\"", "panic");
            Assert.Equal("don't panic, 42", v());
        }

        [Fact]
        public void ShouldEvaluteToKnownVariable() {
            var v1 = Evaluator.ToFunc<bool>("true");
            Assert.True(v1());

            var v2 = Evaluator.ToFunc<bool>("false");
            Assert.False(v2());

            var v3 = Evaluator.ToFunc<object>("null");
            Assert.Null(v3());

            var settings = new Settings().AddKnownValue("secret", 42);
            var v4 = Evaluator.ToFunc<int>("secret", settings);
            Assert.Equal(42, v4());
        }

        [Fact]
        public void ShouldEvaluateVariable() {
            var v1 = Evaluator.ToFunc<string>("Name", new Dictionary<string, object> { { "Name", "Rick" } });
            Assert.Equal("Rick", v1());

            var v2 = Evaluator.ToFunc<string>("@0", "Rick");
            Assert.Equal("Rick", v1());

            var v3 = Evaluator.ToFunc<object>("@0", new object[] { null });
            Assert.Null(v3());
        }

        [Fact]
        public void ShouldEvaluateUnary() {
            var v = Evaluator.ToFunc<bool>("!IsActive", new Dictionary<string, object> { { "IsActive", true } });
            Assert.False(v());

            Assert.Throws<InvalidTokenException>(() => Evaluator.ToLambda<bool>(new UnaryToken('/', new LiteralToken(1))));
        }

        [Fact]
        public void ShouldEvaluateCustomUnary() {
            var settings = new Settings()
                .AddUnaryOperator('^', e => Expression.Multiply(e, e));

            var v = Evaluator.ToFunc<int>("^Id", new Dictionary<string, object> { { "Id", 16 } }, settings);
            Assert.Equal(256, v());
        }

        [Fact]
        public void ShouldEvaluateObject() {
            var v = Evaluator.ToFunc<dynamic>("new { a = 4, b.c }", new Dictionary<string, object> { { "b", new { c = 2 } } });
            var o = v();
            Assert.Equal(4, o.a);
            Assert.Equal(2, o.c);
            var s = o.ToString();
            Assert.Equal("{a=4, c=2}", s);
        }

        [Fact]
        public void ShouldEvaluateArray() {
            var v1 = Evaluator.ToFunc<int[]>("new[] { 4, b.c }", new Dictionary<string, object> { { "b", new { c = 2 } } });
            var a1 = v1();
            Assert.Equal(new[] { 4, 2 }, a1);

            var v2 = Evaluator.ToFunc<object[]>("new [] { }");
            var a2 = v2();
            Assert.Equal(new object[0], a2);
        }

        [Fact]
        public void ShouldEvaluateMember() {
            var v1 = Evaluator.ToFunc<string>("Company.Name", new Dictionary<string, object> { { "Company", new { Name = "Rick" } } });
            Assert.Equal("Rick", v1());

            var v2 = Evaluator.ToFunc<string>("@0.Name", new { Name = "Rick" });
            Assert.Equal("Rick", v2());

            var v3 = Evaluator.ToFunc<Company, string>("Name");
            Assert.Equal("Netflix", v3(new Company { Name = "Netflix" }));

            Assert.Throws<InvalidTokenException>(() => Evaluator.ToFunc<Company, int, string>("Address"));
        }

        [Fact]
        public void ShouldEvaluateIndexer() {
            var name = "Rick";
            var names = new[] { name };
            dynamic user = new ExpandoObject();
            user.Name = name;
            dynamic model = new ExpandoObject();
            model.names = names;
            model.user = user;

            var v1 = Evaluator.ToFunc<string>("names[0]", model);
            Assert.Equal("Rick", v1());

            var v2 = Evaluator.ToFunc<object>("user[\"Name\"]", model);
            Assert.Equal("Rick", v2());

            // should try indexer for missing member access
            var v3 = Evaluator.ToFunc<object>("user.Name", model);
            Assert.Equal("Rick", v3());
        }

        [Fact]
        public void ShouldEvaluateLambda() {
            var v = Evaluator.ToFunc<int, int, bool>("(a, b) => a < b");
            Assert.True(v(1, 2));

            Assert.Throws<InvalidTokenException>(() => Evaluator.ToFunc<int, int, bool>("4 < (a, b) => a < b"));
        }

        [Fact]
        public void ShouldEvaluateCall() {
            var v1 = Evaluator.ToFunc<IEnumerable<int>, int>("items => items.Max()");
            Assert.Equal(5, v1(new[] { 1, 2, 3, 4, 5 }));

            var v2 = Evaluator.ToFunc<IEnumerable<int>, int>("Sum(i => i*2)");
            Assert.Equal(30, v2(new[] { 1, 2, 3, 4, 5 }));

            var v3 = Evaluator.ToFunc<string>("\"RICK\".ToLower()");
            Assert.Equal("rick", v3());

            var v4 = Evaluator.ToFunc<Company, int>("c => c.Len()");
            Assert.Equal(7, v4(new Company { Name = "Netflix" }));

            Assert.Throws<InvalidTokenException>(() => Evaluator.ToFunc<bool>("@0[1]()"));
            Assert.Throws<InvalidTokenException>(() => Evaluator.ToFunc<IEnumerable<int>, int>("SumBody(i => i*2)"));
            Assert.Throws<InvalidOperationException>(() => Evaluator.ToFunc<string, bool>("s => s.GetType()"));
        }

        [Fact]
        public void ShouldEvaluateTernary() {
            var v1 = Evaluator.ToFunc<int>("@0 ? 42 : 21", true);
            Assert.Equal(42, v1());

            var v2 = Evaluator.ToFunc<int>("@0 ? 42 : 21", false);
            Assert.Equal(21, v2());
        }

        [Fact]
        public void ShouldEvaluateBinary() {
            var v1 = Evaluator.ToFunc<bool>("@0 > @1", 4, 2);
            Assert.True(v1());

            var v2 = Evaluator.ToFunc<Company, bool>("c => c.PostalCode < 4");
            Assert.True(v2(new Company { PostalCode = 3 }));

            var id = Guid.NewGuid();
            var v3 = Evaluator.ToFunc<Company, bool>($"c => c.Id == \"{id}\"");
            Assert.True(v3(new Company { Id = id }));

            var ownerId = Guid.NewGuid();
            var v4 = Evaluator.ToFunc<Company, bool>($"c => c.OwnerId == \"{ownerId}\"");
            Assert.True(v4(new Company { OwnerId = ownerId }));

            var createDate = DateTime.Now.Date;
            var v5 = Evaluator.ToFunc<Company, bool>($"c => c.CreateDate == \"{createDate}\"");
            Assert.True(v5(new Company { CreateDate = createDate }));

            var updateDate = DateTime.Now.Date;
            var v6 = Evaluator.ToFunc<Company, bool>($"c => c.UpdateDate == \"{updateDate}\"");
            Assert.True(v6(new Company { UpdateDate = updateDate }));

            var v7 = Evaluator.ToFunc<Company, bool>($"c => c.UpdateDate == null");
            Assert.True(v7(new Company()));

            Assert.Throws<InvalidTokenException>(() => Evaluator.ToLambda<bool>(new BinaryToken("!", new LiteralToken(1), new LiteralToken(2))));
        }

        [Fact]
        public void ShouldEvaluateCustomBinary() {
            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(Company));

            var settings = new Settings()
                .AddBinaryOperator(
                    "in",
                    (l, r) => Expression.Call(containsMethod, new[] { r, l })
                );

            var company1 = new Company();
            var company2 = new Company();
            var companies = new[] { company1 };

            var f = Evaluator.ToFunc<Company, IEnumerable<Company>, bool>("(c, cs) => c in cs", settings);

            Assert.True(f(company1, companies));
            Assert.False(f(company2, companies));
        }

        [Fact]
        public void ShouldEvaluateBinaryWithCorrectPrecedence() {
            var v1 = Evaluator.ToFunc<int>("(1 + 2 * 3)");
            Assert.Equal(7, v1());

            var v2 = Evaluator.ToFunc<int>("(1 * 2 + 3)");
            Assert.Equal(5, v2());
        }

        [Fact]
        public void ShouldEvaluateStaticAccess() {
            var v1 = Evaluator.ToFunc<double>("Math.Round(4.2)");
            Assert.Equal(4, v1());

            var v2 = Evaluator.ToFunc<double>("Math.Ceiling(4.2)");
            Assert.Equal(5, v2());
        }

        [Fact]
        public void ShouldThrowForUnknownToken() {
            Assert.Throws<InvalidTokenException>(() => Evaluator.ToLambda<bool>(new UnkownToken()));
        }

        [Fact]
        public void ShouldThrowForInvalidToken() {
            Assert.Throws<InvalidTokenException>(() => Evaluator.ToLambda<string>(new AssignToken("Name", new LiteralToken("Netflix"))));
            Assert.Throws<InvalidTokenException>(() => Evaluator.ToLambda<string>(new GroupToken(new[] { new LiteralToken("Netflix"), new LiteralToken("Google") })));
            Assert.Throws<InvalidTokenException>(() => Evaluator.ToLambda<string>("a < b => b*2"));
        }

        [Fact]
        public void MethodSignatureTests() {
            var l1 = Evaluator.ToLambda(Tokenizer.Parse("i1 => i1 + i2 + @0"), new Type[] { typeof(int) }, new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, ((Func<int, int>)l1.Compile())(24));

            var l2 = Evaluator.ToLambda(Tokenizer.Parse("i1 => i1 + 17 + @0"), new Type[] { typeof(int) }, 1);
            Assert.Equal(42, ((Func<int, int>)l2.Compile())(24));

            var l3 = Evaluator.ToLambda<int>(Tokenizer.Parse("() => 24 + i2 + @0"), new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, l3.Compile()());

            var l4 = Evaluator.ToLambda<int, int>(Tokenizer.Parse("i1 => i1 + i2 + @0"), new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, l4.Compile()(24));

            var l5 = Evaluator.ToLambda<int, int>(Tokenizer.Parse("i1 => i1 + @0"), 18);
            Assert.Equal(42, l5.Compile()(24));

            var l6 = Evaluator.ToLambda<int, int, int>(Tokenizer.Parse("(i1, i2) => i1 + i2 + init + @0"), new Dictionary<string, object> { { "init", 1 } }, 1);
            Assert.Equal(42, l6.Compile()(24, 16));

            var l7 = Evaluator.ToLambda<int, int, int>(Tokenizer.Parse("(i1, i2) => i1 + i2 + @0"), 1);
            Assert.Equal(42, l7.Compile()(24, 17));

            var l8 = Evaluator.ToLambda("i1 => i1 + i2 + @0", new Type[] { typeof(int) }, new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, ((Func<int, int>)l8.Compile())(24));

            var l9 = Evaluator.ToLambda("i1 => i1 + 17 + @0", new Type[] { typeof(int) }, 1);
            Assert.Equal(42, ((Func<int, int>)l9.Compile())(24));

            var l10 = Evaluator.ToLambda<int>("() => 24 + i2 + @0", new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, l10.Compile()());

            var l11 = Evaluator.ToLambda<int, int>("i1 => i1 + i2 + @0", new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, l11.Compile()(24));

            var l12 = Evaluator.ToLambda<int, int>("i1 => i1 + @0", 18);
            Assert.Equal(42, l12.Compile()(24));

            var l13 = Evaluator.ToLambda<int, int, int>("(i1, i2) => i1 + i2 + init + @0", new Dictionary<string, object> { { "init", 1 } }, 1);
            Assert.Equal(42, l13.Compile()(24, 16));

            var l14 = Evaluator.ToLambda<int, int, int>("(i1, i2) => i1 + i2 + @0", 1);
            Assert.Equal(42, l14.Compile()(24, 17));

            var f1 = Evaluator.ToFunc(Tokenizer.Parse("i1 => i1 + i2 + @0"), new Type[] { typeof(int) }, new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, ((Func<int, int>)f1)(24));

            var f2 = Evaluator.ToFunc(Tokenizer.Parse("i1 => i1 + 17 + @0"), new Type[] { typeof(int) }, 1);
            Assert.Equal(42, ((Func<int, int>)f2)(24));

            var f3 = Evaluator.ToFunc<int>(Tokenizer.Parse("() => 24 + i2 + @0"), new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, f3());

            var f4 = Evaluator.ToFunc<int>(Tokenizer.Parse("() => 24 + @0"), 18);
            Assert.Equal(42, f4());

            var f5 = Evaluator.ToFunc<int, int>(Tokenizer.Parse("i1 => i1 + i2 + @0"), new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, f5(24));

            var f6 = Evaluator.ToFunc<int, int>(Tokenizer.Parse("i1 => i1 + @0"), 18);
            Assert.Equal(42, f6(24));

            var f7 = Evaluator.ToFunc<int, int, int>(Tokenizer.Parse("(i1, i2) => i1 + i2 + init + @0"), new Dictionary<string, object> { { "init", 1 } }, 1);
            Assert.Equal(42, f7(24, 16));

            var f8 = Evaluator.ToFunc<int, int, int>(Tokenizer.Parse("(i1, i2) => i1 + i2 + @0"), 1);
            Assert.Equal(42, f8(24, 17));

            var f9 = Evaluator.ToFunc("i1 => i1 + i2 + @0", new Type[] { typeof(int) }, new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, ((Func<int, int>)f9)(24));

            var f10 = Evaluator.ToFunc("i1 => i1 + 17 + @0", new Type[] { typeof(int) }, 1);
            Assert.Equal(42, ((Func<int, int>)f10)(24));

            var f11 = Evaluator.ToFunc<int, int>("i1 => i1 + i2 + @0", new Dictionary<string, object> { { "i2", 17 } }, 1);
            Assert.Equal(42, f11(24));

            var f12 = Evaluator.ToFunc<int, int, int>("(i1, i2) => i1 + i2 + init + @0", new Dictionary<string, object> { { "init", 1 } }, 1);
            Assert.Equal(42, f12(24, 16));
        }

        [Fact]
        public void SettingTests() {
            var settings = new Settings();

            Assert.Equal(3, settings.KnownIdentifiers.Count());
            Assert.Equal(4, settings.UnaryOperators.Count());
            Assert.Equal(19, settings.BinaryOperators.Count());

            Assert.True(settings.ContainsKnown("true"));
            Assert.True(settings.ContainsUnary('!'));
            Assert.True(settings.ContainsBinary("%"));
        }
    }
}
