using NUnit.Framework;
using UnityEngine;

namespace CircuitAIR.Tests
{
    public class WireRingMathTests
    {
        private const float Ring = 0.03f;
        private const float WireRadius = 0.003f;
        private const float Tube = 0.002f;

        private static WirePath StraightWire()
        {
            // Fio reto de 40 cm ao longo do eixo Z, passando pela origem.
            return WirePath.FromControlPoints(new[] { new Vector3(0f, 0f, -0.2f), new Vector3(0f, 0f, 0.2f) }, 0.02f, 0.002f);
        }

        [Test]
        public void WirePath_IsUniformAndKeepsEndpoints()
        {
            WirePath path = StraightWire();
            Assert.That(path.Length, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(path.Points[0], Is.EqualTo(new Vector3(0f, 0f, -0.2f)));
            Assert.That(path.Points[path.Points.Length - 1], Is.EqualTo(new Vector3(0f, 0f, 0.2f)));
            for (int i = 1; i < path.Points.Length; i++)
                Assert.That(Vector3.Distance(path.Points[i - 1], path.Points[i]), Is.LessThanOrEqualTo(0.00201f));
        }

        [Test]
        public void WirePath_RoundsCorners()
        {
            WirePath path = WirePath.FromControlPoints(
                new[] { new Vector3(-0.1f, 0f, 0f), Vector3.zero, new Vector3(0f, 0.1f, 0f) }, 0.03f, 0.002f);
            // O canto vira curva: o caminho passa longe do vértice original.
            float closest = float.MaxValue;
            foreach (Vector3 p in path.Points)
                closest = Mathf.Min(closest, p.magnitude);
            Assert.That(closest, Is.GreaterThan(0.005f));
            Assert.That(path.Length, Is.LessThan(0.2f));
        }

        [Test]
        public void DistanceToCircle_MatchesGeometry()
        {
            Assert.That(WireRingMath.DistanceToCircle(Vector3.zero, Vector3.zero, Vector3.forward, Ring), Is.EqualTo(Ring).Within(1e-6f));
            Assert.That(WireRingMath.DistanceToCircle(new Vector3(Ring, 0f, 0f), Vector3.zero, Vector3.forward, Ring), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(WireRingMath.DistanceToCircle(new Vector3(Ring, 0f, 0.04f), Vector3.zero, Vector3.forward, Ring), Is.EqualTo(0.04f).Within(1e-6f));
        }

        [Test]
        public void RingCenteredOnWire_IsThreadedAndNotTouching()
        {
            WirePath path = StraightWire();
            RingQuery query = WireRingMath.Query(path, new Vector3(0f, 0f, 0.05f), Vector3.forward, Ring, 0f);
            Assert.IsTrue(query.Threaded);
            Assert.That(query.ThreadDistance, Is.EqualTo(0.25f).Within(0.002f));
            Assert.IsFalse(WireRingMath.IsTouching(query, WireRadius, Tube));
        }

        [Test]
        public void RingPressedAgainstWire_IsTouching()
        {
            WirePath path = StraightWire();
            // O aro está deslocado: o fio fica a 3 mm da borda interna.
            RingQuery query = WireRingMath.Query(path, new Vector3(Ring - 0.003f, 0f, 0f), Vector3.forward, Ring, 0f);
            Assert.IsTrue(query.Threaded);
            Assert.IsTrue(WireRingMath.IsTouching(query, WireRadius, Tube));
        }

        [Test]
        public void RingBesideWire_IsNotThreaded()
        {
            WirePath path = StraightWire();
            RingQuery query = WireRingMath.Query(path, new Vector3(0.08f, 0f, 0f), Vector3.forward, Ring, 0f);
            Assert.IsFalse(query.Threaded);
            Assert.IsFalse(WireRingMath.IsTouching(query, WireRadius, Tube));
        }

        [Test]
        public void TiltedRing_StillThreadsTheWire()
        {
            WirePath path = StraightWire();
            Vector3 tilted = Quaternion.Euler(30f, 0f, 0f) * Vector3.forward;
            RingQuery query = WireRingMath.Query(path, Vector3.zero, tilted, Ring, 0f);
            Assert.IsTrue(query.Threaded);
            Assert.IsFalse(WireRingMath.IsTouching(query, WireRadius, Tube));
        }

        [Test]
        public void ProgressHint_PicksNearestCrossing()
        {
            // Fio em "U": cruza o plano da argola duas vezes, nas duas pernas.
            WirePath path = WirePath.FromControlPoints(new[]
            {
                new Vector3(-0.005f, 0f, -0.1f), new Vector3(-0.005f, 0f, 0.1f),
                new Vector3(0.005f, 0f, 0.1f), new Vector3(0.005f, 0f, -0.1f)
            }, 0.004f, 0.001f);
            RingQuery early = WireRingMath.Query(path, Vector3.zero, Vector3.forward, Ring, 0f);
            RingQuery late = WireRingMath.Query(path, Vector3.zero, Vector3.forward, Ring, path.Length);
            Assert.IsTrue(early.Threaded && late.Threaded);
            Assert.That(early.ThreadDistance, Is.LessThan(late.ThreadDistance));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EveryLevel_CanBeCompletedWithoutTouching(int levelIndex)
        {
            CircuitLevel level = CircuitLevels.Get(levelIndex);
            WirePath path = WirePath.FromControlPoints(level.Points, level.CornerRadius, 0.002f);
            float[] control = path.ControlPointDistances;
            float start = CircuitWire.StartGuideFor(control);
            float finish = CircuitWire.FinishFor(control);
            float tube = Mathf.Max(0.0015f, level.RingRadius * 0.064f);

            Assert.That(finish, Is.GreaterThan(start + 0.1f), "O circuito é curto demais.");

            float worst = float.MaxValue;
            for (float d = start; d <= finish; d += 0.002f)
            {
                Vector3 center = path.PointAt(d);
                Vector3 normal = path.TangentAt(d);
                RingQuery query = WireRingMath.Query(path, center, normal, level.RingRadius, d);
                Assert.IsTrue(query.Threaded, $"Fase {levelIndex + 1}: o fio não passa pela argola em {d:0.000} m.");
                Assert.That(query.ThreadDistance, Is.EqualTo(d).Within(0.01f), $"Fase {levelIndex + 1}: progresso errado em {d:0.000} m.");
                worst = Mathf.Min(worst, query.MinDistance - (level.WireRadius + tube));
            }

            // Folga mínima entre o fio e o aro ao longo de todo o percurso ideal.
            Assert.That(worst, Is.GreaterThan(0.004f), $"Fase {levelIndex + 1}: folga mínima de {worst * 1000f:0.0} mm.");
        }

        /// <summary>
        /// Lupa física: o aro tem o tamanho real (não o da fase). Garante que as três fases podem ser
        /// completadas com aros de arame de 3 a 7 cm de diâmetro e com o aro da equipe
        /// (10 cm por fora, parede de 2 mm, faixa de 1,5 cm de largura).
        /// </summary>
        [Test]
        public void EveryLevel_WorksWithPhysicalLupaSizes(
            [Values(0, 1, 2)] int levelIndex,
            [Values(0.015f, 0.025f, 0.035f, LupaController.DefaultPhysicalRadius)] float ringRadius)
        {
            bool teamRing = Mathf.Approximately(ringRadius, LupaController.DefaultPhysicalRadius);
            float halfDepth = teamRing ? LupaController.DefaultPhysicalHalfDepth : 0f;
            float tube = teamRing ? LupaController.DefaultPhysicalTubeRadius : 0.0015f;

            CircuitLevel level = CircuitLevels.Get(levelIndex);
            WirePath path = WirePath.FromControlPoints(level.Points, level.CornerRadius, 0.002f);
            float[] control = path.ControlPointDistances;
            float start = CircuitWire.StartGuideFor(control);
            float finish = CircuitWire.FinishFor(control);

            float worst = float.MaxValue;
            for (float d = start; d <= finish; d += 0.002f)
            {
                RingQuery query = WireRingMath.Query(path, path.PointAt(d), path.TangentAt(d), ringRadius, d, halfDepth);
                Assert.IsTrue(query.Threaded, $"Fase {levelIndex + 1}, aro {ringRadius * 200f:0.0} cm: fio fora da argola em {d:0.000} m.");
                worst = Mathf.Min(worst, query.MinDistance - (level.WireRadius + tube));
            }
            Assert.That(worst, Is.GreaterThan(0.003f), $"Fase {levelIndex + 1}, aro {ringRadius * 200f:0.0} cm: folga mínima {worst * 1000f:0.0} mm.");
        }

        [Test]
        public void BandRing_TouchesAlongItsWholeWidth()
        {
            const float radius = 0.049f;
            const float halfDepth = 0.0075f;
            // Ponto na parede, 5 mm para o lado ao longo do eixo: ainda está na faixa (distância 0).
            Assert.That(WireRingMath.DistanceToRing(new Vector3(radius, 0f, 0.005f), Vector3.zero, Vector3.forward, radius, halfDepth), Is.EqualTo(0f).Within(1e-6f));
            // 2 cm para o lado: fora da faixa, conta só o que passa da borda (12,5 mm).
            Assert.That(WireRingMath.DistanceToRing(new Vector3(radius, 0f, 0.02f), Vector3.zero, Vector3.forward, radius, halfDepth), Is.EqualTo(0.0125f).Within(1e-6f));
            // Fio inclinado que encosta na borda da faixa, mas não no meio dela, conta como toque.
            WirePath slanted = WirePath.FromControlPoints(new[] { new Vector3(0f, 0f, -0.2f), new Vector3(0.0465f * 2f, 0f, 0.2f) }, 0.02f, 0.001f);
            RingQuery query = WireRingMath.Query(slanted, new Vector3(0f, 0f, -0.007f), Vector3.forward, radius, 0f, halfDepth);
            Assert.IsTrue(WireRingMath.IsTouching(query, 0.003f, 0.001f));
        }

        [Test]
        public void Levels_StartAndEndOnTheTable()
        {
            foreach (CircuitLevel level in CircuitLevels.All)
            {
                Assert.That(level.Points[0].y, Is.EqualTo(0f));
                Assert.That(level.Points[level.Points.Length - 1].y, Is.EqualTo(0f));
                Assert.That(level.RingRadius, Is.GreaterThan(level.WireRadius * 4f));
            }
        }
    }
}
