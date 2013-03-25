﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using NSubstitute;
using NUnit.Framework;
using Neo4jClient.ApiModels.Cypher;
using Neo4jClient.Cypher;

namespace Neo4jClient.Test.Cypher
{
    [TestFixture]
    public class CypherFluentQueryReturnTests
    {
        [Test]
        public void ReturnDistinct()
        {
            var client = Substitute.For<IRawGraphClient>();
            var query = new CypherFluentQuery(client)
                .Start("n", (NodeReference)3)
                .ReturnDistinct<object>("n")
                .Query;

            Assert.AreEqual("START n=node({p0})\r\nRETURN distinct n", query.QueryText);
            Assert.AreEqual(3, query.QueryParameters["p0"]);
        }

        [Test]
        public void ReturnDistinctWithLimit()
        {
            var client = Substitute.For<IRawGraphClient>();
            var query = new CypherFluentQuery(client)
                .Start("n", (NodeReference)3)
                .ReturnDistinct<object>("n")
                .Limit(5)
                .Query;

            Assert.AreEqual("START n=node({p0})\r\nRETURN distinct n\r\nLIMIT {p1}", query.QueryText);
            Assert.AreEqual(3, query.QueryParameters["p0"]);
            Assert.AreEqual(5, query.QueryParameters["p1"]);
        }

        [Test]
        public void ReturnDistinctWithLimitAndOrderBy()
        {
            var client = Substitute.For<IRawGraphClient>();
            var query = new CypherFluentQuery(client)
                .Start("n", (NodeReference)3)
                .ReturnDistinct<object>("n")
                .OrderBy("n.Foo")
                .Limit(5)
                .Query;

            Assert.AreEqual("START n=node({p0})\r\nRETURN distinct n\r\nORDER BY n.Foo\r\nLIMIT {p1}", query.QueryText);
            Assert.AreEqual(3, query.QueryParameters["p0"]);
            Assert.AreEqual(5, query.QueryParameters["p1"]);
        }

        [Test]
        public void Return()
        {
            var client = Substitute.For<IRawGraphClient>();
            var query = new CypherFluentQuery(client)
                .Start("n", (NodeReference)3)
                .Return<object>("n")
                .Query;

            Assert.AreEqual("START n=node({p0})\r\nRETURN n", query.QueryText);
            Assert.AreEqual(3, query.QueryParameters["p0"]);
        }

        [Test]
        public void ReturnWithLimit()
        {
            var client = Substitute.For<IRawGraphClient>();
            var query = new CypherFluentQuery(client)
                .Start("n", (NodeReference)3)
                .Return<object>("n")
                .Limit(5)
                .Query;

            Assert.AreEqual("START n=node({p0})\r\nRETURN n\r\nLIMIT {p1}", query.QueryText);
            Assert.AreEqual(3, query.QueryParameters["p0"]);
            Assert.AreEqual(5, query.QueryParameters["p1"]);
        }

        [Test]
        public void ReturnWithLimitAndOrderBy()
        {
            var client = Substitute.For<IRawGraphClient>();
            var query = new CypherFluentQuery(client)
                .Start("n", (NodeReference)3)
                .Return<object>("n")
                .OrderBy("n.Foo")
                .Limit(5)
                .Query;

            Assert.AreEqual("START n=node({p0})\r\nRETURN n\r\nORDER BY n.Foo\r\nLIMIT {p1}", query.QueryText);
            Assert.AreEqual(3, query.QueryParameters["p0"]);
            Assert.AreEqual(5, query.QueryParameters["p1"]);
        }

        [Test]
        public void Issue42()
        {
            var client = Substitute.For<IRawGraphClient>();
            var query = new CypherFluentQuery(client)
                .Start(
                    new CypherStartBit("me", (NodeReference)123),
                    new CypherStartBit("viewer", (NodeReference)456)
                )
                .Match("me-[:FRIEND]-common-[:FRIEND]-viewer")
                .Return<Node<object>>("common")
                .Limit(5)
                .OrderBy("common.FirstName")
                .Query;

            Assert.AreEqual(@"START me=node({p0}), viewer=node({p1})
MATCH me-[:FRIEND]-common-[:FRIEND]-viewer
RETURN common
LIMIT {p2}
ORDER BY common.FirstName", query.QueryText);
            Assert.AreEqual(123, query.QueryParameters["p0"]);
            Assert.AreEqual(456, query.QueryParameters["p1"]);
            Assert.AreEqual(5, query.QueryParameters["p2"]);
        }

        [Test]
        public void ShouldUseSetResultModeForIdentityBasedReturn()
        {
            var client = Substitute.For<IRawGraphClient>();
            var query = new CypherFluentQuery(client)
                .Return<object>("foo")
                .Query;

            Assert.AreEqual(CypherResultMode.Set, query.ResultMode);
        }

        [Test]
        public void ShouldUseProjectionResultModeForLambdaBasedReturn()
        {
            var client = Substitute.For<IRawGraphClient>();
            var query = new CypherFluentQuery(client)
                .Return(a => new { Foo = a.As<object>() })
                .Query;

            Assert.AreEqual(CypherResultMode.Projection, query.ResultMode);
        }

        [Test]
        public void ShouldSupportAnonymousReturnTypesEndToEnd()
        {
            const string queryText = "START root=node({p0})\r\nMATCH root-->other\r\nRETURN other AS Foo";
            var parameters = new Dictionary<string, object>
            {
                {"p0", 123}
            };

            var cypherQuery = new CypherQuery(queryText, parameters, CypherResultMode.Projection);
            var cypherApiQuery = new CypherApiQuery(cypherQuery);

            using (var testHarness = new RestTestHarness
            {
                {
                    MockRequest.PostObjectAsJson("/cypher", cypherApiQuery),
                    MockResponse.Json(HttpStatusCode.OK, @"{
                        'data' : [
                            [ 1 ],
                            [ 2 ],
                            [ 3 ]
                        ],
                        'columns' : [ 'Foo' ]
                    }")
                }
            })
            {
                var graphClient = testHarness.CreateAndConnectGraphClient();

                var results = graphClient
                    .Cypher
                    .Start("root", graphClient.RootNode)
                    .Match("root-->other")
                    .Return(other => new
                    {
                        Foo = other.As<int>()
                    })
                    .Results
                    .ToList();

                Assert.AreEqual(3, results.Count());

                var result = results[0];
                Assert.AreEqual(1, result.Foo);

                result = results[1];
                Assert.AreEqual(2, result.Foo);

                result = results[2];
                Assert.AreEqual(3, result.Foo);
            }
        }
    }
}
