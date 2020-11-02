namespace UnitTestingNeo4jDriver4
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Moq;
    using Neo4j.Driver;
    using Xunit;

    public class MovieStoreTests_Part2
    {
        private static Mock<INode> GetMockNode(string title = "Title", string tagline = "Tagline", int? released = 2000)
        {
            var nodeMock = new Mock<INode>();
            nodeMock.Setup(x => x.Properties["title"]).Returns(title);
            nodeMock.Setup(x => x.Properties["tagline"]).Returns(tagline);
            nodeMock.Setup(x => x.Properties["released"]).Returns(released);
            return nodeMock;
        }

        private static SessionConfigBuilder GenerateSessionConfigBuilder()
        {
            var sc = FormatterServices.GetUninitializedObject(typeof(SessionConfig)) as SessionConfig;

            var ctr = typeof(SessionConfigBuilder)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(c => c.GetParameters().Length == 1 && c.GetParameters().Single().ParameterType == typeof(SessionConfig));

            return ctr.Invoke(new object[] {sc}) as SessionConfigBuilder;
        }

        private static void GetMocks(
            out Mock<IDriver> driver, 
            out Mock<IAsyncSession> session, 
            out Mock<IAsyncTransaction> transaction, 
            out Mock<IResultCursor> cursor,
            out SessionConfigBuilder sessionConfigBuilder)
        {
            var transactionMock = new Mock<IAsyncTransaction>();
            var sessionMock = new Mock<IAsyncSession>();
            sessionMock
                .Setup(x => x.ReadTransactionAsync(It.IsAny<Func<IAsyncTransaction, Task<List<Movie>>>>()))
                .Returns((Func<IAsyncTransaction, Task<List<Movie>>> func) => { return func(transactionMock.Object); });

            var cursorMock = new Mock<IResultCursor>();
            transactionMock
                .Setup(x => x.RunAsync(It.IsAny<string>(), It.IsAny<object>()))
                .Returns(Task.FromResult(cursorMock.Object));

            var builder = GenerateSessionConfigBuilder();
            var driverMock = new Mock<IDriver>();
            driverMock
                .Setup(x => x.AsyncSession(It.IsAny<Action<SessionConfigBuilder>>()))
                .Returns((Action<SessionConfigBuilder> action) =>
                {
                    action(builder);
                    return sessionMock.Object;
                });

            driver = driverMock;
            session = sessionMock;
            transaction = transactionMock;
            cursor = cursorMock;
            sessionConfigBuilder = builder;
        }

        private static bool CompareParameters<T>(object o, T expectedValue, string propertyName)
        {
            var actualValue = (T) o.GetType().GetProperty(propertyName)?.GetValue(o);
            actualValue.Should().Be(expectedValue);
            return true;
        }

        public class GetMovie_Part2Method
        {
            [Fact]
            public async Task Test1_ReturnsEmptyCollection_WhenInvalidTitleGiven()
            {
                GetMocks(out var driverMock, out _, out _, out _, out _);
                var movieStore = new MovieStore(driverMock.Object);
                var actual = await movieStore.GetMovie_Part2("invalid");

                actual.Should().BeEmpty();
            }

            [Fact]
            public async Task Test2_UsesTheAsyncSession_ToGetTheMovie()
            {
                GetMocks(out var driverMock, out _, out _, out _, out _);
                var movieStore = new MovieStore(driverMock.Object);
                await movieStore.GetMovie_Part2("valid");

                driverMock.Verify(x => x.AsyncSession(It.IsAny<Action<SessionConfigBuilder>>()), Times.Once);
            }

            [Fact]
            public async Task Test2a_ClosesTheAsyncSession_ToGetTheMovie()
            {
                GetMocks(out var driverMock, out var sessionMock, out _, out _, out _);

                var movieStore = new MovieStore(driverMock.Object);
                await movieStore.GetMovie_Part2("valid");

                sessionMock.Verify(x => x.CloseAsync(), Times.Once);
            }

            [Fact]
            public async Task Test3_OpensAReadTransaction()
            {
                GetMocks(out var driverMock, out var sessionMock, out _, out _, out _);

                var movieStore = new MovieStore(driverMock.Object);
                await movieStore.GetMovie_Part2("valid");

                sessionMock.Verify(x => x.ReadTransactionAsync(It.IsAny<Func<IAsyncTransaction, Task<List<Movie>>>>()), Times.Once);
            }

            [Fact]
            public async Task Test4_ExecutesTheRightCypher()
            {
                const string expectedCypher = "MATCH (m:Movie) WHERE m.title = $title RETURN m";
                GetMocks(out var driverMock, out _, out var transactionMock, out _, out _);

                var movieStore = new MovieStore(driverMock.Object);
                await movieStore.GetMovie_Part2("valid");

                transactionMock.Verify(x => x.RunAsync(expectedCypher, It.IsAny<object>()), Times.Once);
            }

            [Fact]
            public async Task Test4a_ExecutesUsingTheRightParameter()
            {
                const string expectedParameter = "valid";
                GetMocks(out var driverMock, out _, out var transactionMock, out _, out _);

                var movieStore = new MovieStore(driverMock.Object);
                await movieStore.GetMovie_Part2(expectedParameter);

                transactionMock.Verify(x => x.RunAsync(It.IsAny<string>(), It.Is<object>(o => CompareParameters(o, expectedParameter, "title"))), Times.Once);
            }

            [Fact]
            public async Task Test5_CallsFetchAsyncToGetTheNextRecord()
            {
                GetMocks(out var driverMock, out _, out _, out var cursorMock, out _);

                var movieStore = new MovieStore(driverMock.Object);
                await movieStore.GetMovie_Part2("Valid");

                cursorMock.Verify(x => x.FetchAsync(), Times.Once);
            }

            [Fact]
            public async Task Test5a_AttemptsToGetTheData()
            {
                GetMocks(out var driverMock, out _, out var transactionMock, out _, out _);

                var nodeMock = GetMockNode();
                var cursorMock = new Mock<IResultCursor>();
                cursorMock.Setup(x => x.Current["m"])
                    .Returns(nodeMock.Object);

                cursorMock
                    .SetupSequence(x => x.FetchAsync())
                    .Returns(Task.FromResult(true))
                    .Returns(Task.FromResult(false));

                transactionMock
                    .Setup(x => x.RunAsync(It.IsAny<string>(), It.IsAny<object>()))
                    .Returns(Task.FromResult(cursorMock.Object));

                var movieStore = new MovieStore(driverMock.Object);
                await movieStore.GetMovie_Part2("Valid");

                nodeMock.Verify(x => x.Properties["title"], Times.Once);
                nodeMock.Verify(x => x.Properties["tagline"], Times.Once);
                nodeMock.Verify(x => x.Properties["released"], Times.Once);
            }


            [Fact]
            public async Task Test6_CallsFetchAsyncUntilFalseReturned()
            {
                GetMocks(out var driverMock, out _, out var transactionMock, out _, out _);

                var nodeMock = GetMockNode();
                var cursorMock = new Mock<IResultCursor>();
                cursorMock.Setup(x => x.Current["m"])
                    .Returns(nodeMock.Object);

                cursorMock
                    .SetupSequence(x => x.FetchAsync())
                    .Returns(Task.FromResult(true))
                    .Returns(Task.FromResult(false));

                transactionMock
                    .Setup(x => x.RunAsync(It.IsAny<string>(), It.IsAny<object>()))
                    .Returns(Task.FromResult(cursorMock.Object));

                var movieStore = new MovieStore(driverMock.Object);
                await movieStore.GetMovie_Part2("Valid");

                cursorMock.Verify(x => x.FetchAsync(), Times.Exactly(2));
            }

            [Fact]
            public async Task Test7_ReturnsTheMovie()
            {
                GetMocks(out var driverMock, out _, out var transactionMock, out _, out _);

                const string expectedTitle = "Foo";
                const string expectedTagline = "Bar";
                const int expectedReleased = 1900;

                var nodeMock = GetMockNode(expectedTitle, expectedTagline, expectedReleased);
                var cursorMock = new Mock<IResultCursor>();
                cursorMock.Setup(x => x.Current["m"])
                    .Returns(nodeMock.Object);

                cursorMock
                    .SetupSequence(x => x.FetchAsync())
                    .Returns(Task.FromResult(true))
                    .Returns(Task.FromResult(false));

                transactionMock
                    .Setup(x => x.RunAsync(It.IsAny<string>(), It.IsAny<object>()))
                    .Returns(Task.FromResult(cursorMock.Object));

                var movieStore = new MovieStore(driverMock.Object);
                var movies = (await movieStore.GetMovie_Part2("Valid")).ToList();

                movies.Should().HaveCount(1);
                var movie = movies.First();
                movie.Title.Should().Be(expectedTitle);
                movie.Tagline.Should().Be(expectedTagline);
                movie.Released.Should().Be(expectedReleased);
            }

            [Fact]
            public async Task Part2_Test1_UsesTheCorrectDatabase()
            {
                const string expectedDb = "movies";
                GetMocks(out var mockDriver, out _, out _, out _, out var builder);
                var movieStore = new MovieStore(mockDriver.Object);
                await movieStore.GetMovie_Part2("Valid");

                var config = builder.Build();
                config.Database.Should().Be(expectedDb);

            }
        }
    }

    public static class SessionConfigBuilderExtensions
    {
        public static SessionConfig Build(this SessionConfigBuilder scb)
        {
            var buildMethod = typeof(SessionConfigBuilder).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Instance);
            return buildMethod?.Invoke(scb, null) as SessionConfig;
        }
    }
}