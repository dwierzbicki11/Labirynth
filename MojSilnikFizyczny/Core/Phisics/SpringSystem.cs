using System.Runtime.InteropServices;

namespace Core.Physics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct JointLink
    {
        public int TargetParticleID; // Indeks połączonej tratwy (-1 oznacza brak połączenia)
        public float RestLength;     // Dystans spoczynkowy sprężyny
        public float Stiffness;      // Współczynnik sztywności (Hooke's Law)
        public float Damping;        // Tłumienie drgań sprężyny
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ParticleJoints
    {
        // Każdy obiekt może mieć do 2 niezależnych sztywnych fizycznych połączeń (np. tworząc sieć lub linę)
        public JointLink Link0;
        public JointLink Link1;
    }
}