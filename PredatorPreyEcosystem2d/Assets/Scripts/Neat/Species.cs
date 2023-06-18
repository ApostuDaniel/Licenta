using System.Collections.Generic;

namespace Assets.Scripts.Neat
{
    public class Species
    {
        public int Id { get; set; }
        public List<int> MembersIdx { get; set; }
        public NeatGenome SpeciesRepresentative { get; set; }
        public int PotentialMembersCount { get; set; }
        public float LastBestFitness { get; set; }
        public int GenerationsSinceImprovement { get; set; }
        public int GenerationsSinceMemebersAssigned { get; set; }
        public float SpeciesAdjustedFitness { get; set; }

        public (byte r, byte g, byte b) RgbColor { get; set; }

        public Species(int id)
        {
            Id = id;
            MembersIdx = new List<int>();
            RgbColor = Utils.CreateColorFromId(id);
        }

        public string GetStringColor()
        {
            (var r, var g, var b) = RgbColor;
            return r.ToString("x2") + g.ToString("x2") + b.ToString("x2");
        }
    }
}