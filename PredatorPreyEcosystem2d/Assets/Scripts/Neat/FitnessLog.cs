using CsvHelper;
using CsvHelper.Configuration.Attributes;

namespace Assets.Scripts.Neat
{
    public class FitnessLog
    {
        [Name("generation")]
        public int Generation { get; set; }
        [Name("average_fitness")]
        public float AverageFitness { get; set; }
        [Name("best_fitness")]
        public float BestFitness { get; set; }
        [Name("best_hidden_nodes")]
        public int HiddenNodes { get; set; }
        [Name("best_nr_connections")]
        public int BestNrConnections { get; set; }
    }
}