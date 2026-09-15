using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Presentation.Tests
{
    public sealed class SpriteAnimatorTests
    {
        private readonly List<UnityEngine.Object> owned = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index] != null)
                    UnityEngine.Object.DestroyImmediate(owned[index]);
            }
            owned.Clear();
        }

        [Test]
        public void PlayIfChanged_SameClip_DoesNotResetElapsedTime()
        {
            var animator = CreateAnimator(out var first, out var second);
            var clip = new VisualClip("idle", new[] { first, second }, 4f, true, first);

            animator.PlayIfChanged(clip);
            animator.Tick(0.5f);
            var elapsed = animator.ElapsedForTests;

            animator.PlayIfChanged(clip);

            Assert.That(animator.ElapsedForTests, Is.EqualTo(elapsed));
        }

        [Test]
        public void Tick_LoopingClip_AdvancesAndWrapsFrames()
        {
            var animator = CreateAnimator(out var first, out var second);
            var renderer = animator.GetComponent<SpriteRenderer>();
            var clip = new VisualClip("walk", new[] { first, second }, 10f, true, first);

            animator.Play(clip);
            animator.Tick(0.05f);
            Assert.That(renderer.sprite, Is.SameAs(first));

            animator.Tick(0.05f);
            Assert.That(renderer.sprite, Is.SameAs(second));
            Assert.That(animator.IsPlaying, Is.True);

            animator.Tick(0.1f);
            Assert.That(renderer.sprite, Is.SameAs(first));
            Assert.That(animator.IsPlaying, Is.True);
        }

        [Test]
        public void Tick_NonLoopingClip_StopsOnLastFrame()
        {
            var animator = CreateAnimator(out var first, out var second);
            var renderer = animator.GetComponent<SpriteRenderer>();
            var clip = new VisualClip("hit", new[] { first, second }, 10f, false, first);

            animator.Play(clip);
            animator.Tick(0.2f);

            Assert.That(renderer.sprite, Is.SameAs(second));
            Assert.That(animator.IsPlaying, Is.False);

            animator.Tick(0.2f);
            Assert.That(renderer.sprite, Is.SameAs(second));
        }

        [Test]
        public void Play_NewClip_ResetsElapsedTimeAndShowsFirstFrame()
        {
            var animator = CreateAnimator(out var first, out var second);
            var renderer = animator.GetComponent<SpriteRenderer>();
            animator.Play(new VisualClip("first", new[] { first, second }, 10f, true, first));
            animator.Tick(0.15f);

            animator.Play(new VisualClip("second", new[] { second, first }, 4f, true, second));

            Assert.That(animator.CurrentClipId, Is.EqualTo("second"));
            Assert.That(animator.ElapsedForTests, Is.EqualTo(0f));
            Assert.That(renderer.sprite, Is.SameAs(second));
            Assert.That(animator.IsPlaying, Is.True);
        }

        [Test]
        public void Play_WithoutSpriteRenderer_ThrowsInvalidOperationException()
        {
            var root = new GameObject("animator");
            owned.Add(root);
            var animator = root.AddComponent<SpriteAnimator>();
            var clip = new VisualClip("idle", Array.Empty<Sprite>(), 1f, false, null);

            Assert.Throws<InvalidOperationException>(() => animator.Play(clip));
        }

        [Test]
        public void SpriteAnimator_DisallowsMultipleComponents()
        {
            Assert.That(
                Attribute.IsDefined(typeof(SpriteAnimator), typeof(DisallowMultipleComponent)),
                Is.True);
        }

        private SpriteAnimator CreateAnimator(out Sprite first, out Sprite second)
        {
            var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            owned.Add(texture);
            first = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f, 2f);
            owned.Add(first);
            second = Sprite.Create(texture, new Rect(2f, 0f, 2f, 2f), Vector2.one * 0.5f, 2f);
            owned.Add(second);

            var root = new GameObject("animator", typeof(SpriteRenderer), typeof(SpriteAnimator));
            owned.Add(root);
            return root.GetComponent<SpriteAnimator>();
        }
    }
}
