﻿using System;

namespace osuElements.Beatmaps.Difficulty
{
    internal class StandardDifficultyHitObject
    {
        public static readonly double[] DECAY_BASE = { 0.3, 0.15 };

        private static readonly double[] SPACING_WEIGHT_SCALING = { 1400, 26.25 };
        private const int LAZY_SLIDER_STEP_LENGTH = 10;
        private const float CIRCLESIZE_BUFF_TRESHOLD = 30f;

        public HitObject BaseHitObject;
        public double[] Strains = { 1, 1 };
        private readonly Position _normalizedStartPosition;
        private readonly Position _normalizedEndPosition;
        private readonly double _lazySliderLengthFirst;
        private readonly double _lazySliderLengthSubsequent;

        public StandardDifficultyHitObject(HitObject baseHitObject, float radius) {
            BaseHitObject = baseHitObject;
            var scalingFactor = 52f / radius;

            if (radius < CIRCLESIZE_BUFF_TRESHOLD) { //CS ~5.5 -> ~6.5, up to 10% increase
                scalingFactor *= Math.Min(1.1f, 1 + (CIRCLESIZE_BUFF_TRESHOLD - radius) * 0.02f);
            }
            _normalizedStartPosition = baseHitObject.StartPosition * scalingFactor;

            if (baseHitObject.IsHitObjectType(HitObjectType.Slider)) {

                var slider = (Slider)baseHitObject;
                var sliderFollowCircleRadius = radius * 3f;

                var segmentLength = Math.Min(slider.SegmentDuration, 60000);
                var segmentEndTime = slider.StartTime + segmentLength;

                var cursorPos = slider.StartPosition;

                for (var time = slider.StartTime + LAZY_SLIDER_STEP_LENGTH;
                    time < segmentEndTime; time += LAZY_SLIDER_STEP_LENGTH) {
                    var difference = slider.PositionAtTime(time) - cursorPos;
                    var distance = difference.Length;

                    if (distance <= sliderFollowCircleRadius) continue;
                    difference = difference.Normalize();
                    distance -= sliderFollowCircleRadius;
                    cursorPos += difference * distance;
                    _lazySliderLengthFirst += distance;
                }

                _lazySliderLengthFirst *= scalingFactor;
                if (slider.SegmentCount % 2 == 1) {
                    _normalizedEndPosition = cursorPos * scalingFactor;
                }

                if (slider.SegmentCount <= 1) return;

                segmentEndTime += segmentLength;
                for (var time = segmentEndTime - segmentLength + LAZY_SLIDER_STEP_LENGTH;
                    time < segmentEndTime; time += LAZY_SLIDER_STEP_LENGTH) {
                    var difference = slider.PositionAtTime((int)time) - cursorPos;
                    var distance = (float)difference.Length;

                    if (distance <= sliderFollowCircleRadius) continue;
                    difference.Normalize();
                    distance -= sliderFollowCircleRadius;
                    cursorPos += difference * distance;
                    _lazySliderLengthSubsequent += distance;
                }

                if (slider.SegmentCount % 2 != 0) return;
                _normalizedEndPosition = cursorPos * scalingFactor;

            }
            else {
                _normalizedEndPosition = _normalizedStartPosition;
            }
        }

        public void CalculateStrains(StandardDifficultyHitObject previousHitObject, double speedMultiplier) {
            CalculateSpecificStrain(previousHitObject, DifficultyType.Speed, speedMultiplier);
            CalculateSpecificStrain(previousHitObject, DifficultyType.Aim, speedMultiplier);
        }

        private static double SpacingWeight(double distance, DifficultyType type) {
            const double ALMOST_DIAMETER = 90d;
            const double HALF_ALMOST_DIAMETER = 45d;
            const double STREAM_SPACING_TRESHOLD = 110d;
            const double SINGLE_SPACING_TRESHOLD = 125d;

            if (type != DifficultyType.Speed) return type == DifficultyType.Aim ? Math.Pow(distance, 0.99) : 0;

            double weight;
            if (distance > SINGLE_SPACING_TRESHOLD)
                weight = 2.5;
            else if (distance > STREAM_SPACING_TRESHOLD)
                weight = 1.6 + 0.9 * (distance - STREAM_SPACING_TRESHOLD) / 15d;
            else if (distance > ALMOST_DIAMETER)
                weight = 1.2 + 0.4 * (distance - ALMOST_DIAMETER) * 0.05;
            else if (distance > HALF_ALMOST_DIAMETER)
                weight = 0.95 + 0.25 * (distance - HALF_ALMOST_DIAMETER) / HALF_ALMOST_DIAMETER;
            else
                weight = 0.95;

            return weight;
        }


        private void CalculateSpecificStrain(StandardDifficultyHitObject previousHitObject, DifficultyType type, double speedMultiplier) {
            var addition = .0;
            var timeElapsed = (BaseHitObject.StartTime - previousHitObject.BaseHitObject.StartTime) / speedMultiplier;
            var decay = Math.Pow(DECAY_BASE[(int)type], timeElapsed * 0.001);

            switch (BaseHitObject.Type) {
                case HitObjectType.Slider:
                    switch (type) {
                        case DifficultyType.Speed:

                            addition =
                                SpacingWeight(previousHitObject._lazySliderLengthFirst +
                                              previousHitObject._lazySliderLengthSubsequent * (previousHitObject.BaseHitObject.SegmentCount - 1) +
                                              DistanceTo(previousHitObject), type) *
                                SPACING_WEIGHT_SCALING[(int)type];
                            break;


                        case DifficultyType.Aim:

                            addition =
                                (
                                    SpacingWeight(previousHitObject._lazySliderLengthFirst, type) +
                                    SpacingWeight(previousHitObject._lazySliderLengthSubsequent, type) * (previousHitObject.BaseHitObject.SegmentCount - 1) +
                                    SpacingWeight(DistanceTo(previousHitObject), type)
                                    ) *
                                SPACING_WEIGHT_SCALING[(int)type];
                            break;
                    }
                    break;
                case HitObjectType.HitCircle:
                    addition = SpacingWeight(DistanceTo(previousHitObject), type) * SPACING_WEIGHT_SCALING[(int)type];
                    break;
            }

            addition /= Math.Max(timeElapsed, 50);

            Strains[(int)type] = previousHitObject.Strains[(int)type] * decay + addition;
        }



        public float DistanceTo(StandardDifficultyHitObject other) =>
            (float)(_normalizedStartPosition - other._normalizedEndPosition).Length;

    }
}
