namespace UnitTestingNeo4jDriver4
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Neo4j.Driver;

    public class MovieStore
    {
        private readonly IDriver _driver;

        public MovieStore(IDriver driver)
        {
            _driver = driver;
        }

        public async Task<IEnumerable<Movie>> GetMovie(string title)
        {
            const string query = "MATCH (m:Movie) WHERE m.title = $title RETURN m";

            var session = _driver.AsyncSession();
            var results = await session.ReadTransactionAsync(async tx =>
            {
                var cursor = await tx.RunAsync(query, new {title});
                var fetched = await cursor.FetchAsync();

                var output = new List<Movie>();
                while (fetched)
                {
                    var node = cursor.Current["m"].As<INode>();

                    var movie = new Movie
                    {
                        Title = node.Properties["title"].As<string>(), 
                        Tagline = node.Properties["tagline"].As<string>(), 
                        Released = node.Properties["released"].As<int>()
                    };

                    output.Add(movie);
                    fetched = await cursor.FetchAsync();
                }

                return output;
            });

            await session.CloseAsync();
            return results;
        }

        public async Task<IEnumerable<Movie>> GetMovie_Part2(string title)
        {
            const string query = "MATCH (m:Movie) WHERE m.title = $title RETURN m";

            var session = _driver.AsyncSession(builder => builder.WithDatabase("movies"));
            var results = await session.ReadTransactionAsync(async tx =>
            {
                var cursor = await tx.RunAsync(query, new {title});
                var fetched = await cursor.FetchAsync();

                var output = new List<Movie>();
                while (fetched)
                {
                    var node = cursor.Current["m"].As<INode>();

                    var movie = new Movie
                    {
                        Title = node.Properties["title"].As<string>(), 
                        Tagline = node.Properties["tagline"].As<string>(), 
                        Released = node.Properties["released"].As<int>()
                    };

                    output.Add(movie);
                    fetched = await cursor.FetchAsync();
                }

                return output;
            });

            await session.CloseAsync();
            return results;
        }
    }
}