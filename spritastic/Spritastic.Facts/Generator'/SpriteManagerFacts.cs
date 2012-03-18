﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.IO;
using Moq;
using Spritastic;
using Spritastic.Facts.Utilities;
using Spritastic.Generator;
using Spritastic.ImageLoad;
using Spritastic.Parser;
using Spritastic.SpriteStore;
using Spritastic.Utilities;
using Xunit;
using Xunit.Extensions;

namespace Spriting.Facts
{
    public class SpriteManagerFacts
    {
        static class TestImages
        {
            public static readonly Bitmap Image15X17 = CreateFileImage(string.Format("{0}\\testimages\\delete.png", AppDomain.CurrentDomain.BaseDirectory));
            public static readonly Bitmap Image18X18 = CreateFileImage(string.Format("{0}\\testimages\\emptyStar.png", AppDomain.CurrentDomain.BaseDirectory));

            static Bitmap CreateFileImage(string path)
            {
                return new Bitmap(new MemoryStream(File.ReadAllBytes(path)));
            }
        }

        class SpriteManagerToTest : SpriteManager
        {
            public Mock<ISpriteContainer> MockSpriteContainer { get; private set; }
            public new ISpriteContainer SpriteContainer { get { return base.SpriteContainer; } set { base.SpriteContainer = value; } }
            public SpriteManagerToTest(ISpritingSettings config, IImageLoader imageLoader, ISpriteStore spriteStore, IPngOptimizer pngOptimizer)
                : base(config, imageLoader, spriteStore, pngOptimizer)
            {
                MockSpriteContainer = new Mock<ISpriteContainer>();
                MockSpriteContainer.Setup(x => x.GetEnumerator()).Returns(new List<SpritedImage>().GetEnumerator());
                MockSpriteContainer.Setup(x => x.Width).Returns(1);
                MockSpriteContainer.Setup(x => x.Height).Returns(1);
                MockSpriteContainer.Setup(x => x.AddImage(It.IsAny<BackgroundImageClass>())).
                    Returns((BackgroundImageClass c) => new SpritedImage(1, c, null));
                base.SpriteContainer = MockSpriteContainer.Object;
            }
        }

        class TestableSpriteManager : Testable<SpriteManagerToTest>
        {
            public readonly ISpritingSettings Settings = new SpritingSettings
                                                    {
                                                        SpriteSizeLimit = 1000,
                                                        SpriteColorLimit = 1000,
                                                        IsFullTrust = true
                                                    };

            public TestableSpriteManager()
            {
                Inject(Settings);
                Mock<ISpriteStore>().Setup(x => x.SaveSpriteAndReturnUrl(It.IsAny<byte[]>())).Returns("url");

            }
        }

        public class Add
        {
            [Fact]
            public void WillAddImageToSpriteContainer()
            {
                var testable = new TestableSpriteManager();
                var image = new BackgroundImageClass("", 0){ImageUrl = ""};

                testable.ClassUnderTest.Add(image);

                testable.ClassUnderTest.MockSpriteContainer.Verify(x => x.AddImage(image), Times.Exactly(1));
            }

            [Fact]
            public void WillMarkImageAsSpriteIfUrlIsReferencedInAnotherImage()
            {
                var testable = new TestableSpriteManager();
                var image1 = new BackgroundImageClass("", 0) {ImageUrl = "url1"};
                var image2 = new BackgroundImageClass("", 0) {ImageUrl = "url2"};
                var image3 = new BackgroundImageClass("", 0) {ImageUrl = "url1", XOffset = new Position {Offset = -100}};

                testable.ClassUnderTest.Add(image1);
                testable.ClassUnderTest.Add(image2);
                testable.ClassUnderTest.Add(image3);

                Assert.True(image1.IsSprite);
                Assert.True(image3.IsSprite);
                Assert.False(image2.IsSprite);
            }

            [Fact]
            public void WillFlushWhenSizePassesThreshold()
            {
                var testable = new TestableSpriteManager();
                testable.Settings.SpriteSizeLimit = 1;
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);

                testable.ClassUnderTest.Add(new BackgroundImageClass("", 0) { ImageUrl = "imageUrl" });

                Assert.NotEqual(testable.ClassUnderTest.MockSpriteContainer.Object, testable.ClassUnderTest.SpriteContainer);
            }

            [Fact]
            public void WillFlushWhenColorCountPassesThreshold()
            {
                var testable = new TestableSpriteManager();
                testable.Settings.SpriteColorLimit = 1;
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Colors).Returns(1);

                testable.ClassUnderTest.Add(new BackgroundImageClass("", 0) { ImageUrl = "imageUrl" });

                Assert.NotEqual(testable.ClassUnderTest.MockSpriteContainer.Object, testable.ClassUnderTest.SpriteContainer);
            }

            [Fact]
            public void WillNotFlushWhenColorCountPassesThresholdAndImageOptimizationIsDisabed()
            {
                var testable = new TestableSpriteManager();
                testable.Settings.SpriteSizeLimit = 1;
                testable.Settings.ImageOptimizationDisabled = true;
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Colors).Returns(1);

                testable.ClassUnderTest.Add(new BackgroundImageClass("", 0) { ImageUrl = "imageUrl" });

                Assert.Same(testable.ClassUnderTest.MockSpriteContainer.Object, testable.ClassUnderTest.SpriteContainer);
            }

            [Fact]
            public void WillNotFlushWhenColorCountPassesThresholdAndImageQuantizationIsDisabed()
            {
                var testable = new TestableSpriteManager();
                testable.Settings.SpriteSizeLimit = 1;
                testable.Settings.ImageQuantizationDisabled = true;
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Colors).Returns(1);

                testable.ClassUnderTest.Add(new BackgroundImageClass("", 0) { ImageUrl = "imageUrl" });

                Assert.Same(testable.ClassUnderTest.MockSpriteContainer.Object, testable.ClassUnderTest.SpriteContainer);
            }

            [Theory,
            InlineData(40, 40, 0, 50, 40, 0),
            InlineData(40, 40, 0, 40, 50, 0),
            InlineData(40, 40, 0, 40, 40, -10)]
            public void WillTreatSameUrlwithDifferentWithHeightOrXOffsetAsDifferentImagesAndReturnDistinctSprite(int image1Width, int image1Height, int image1XOffset, int image2Width, int image2Height, int image2XOffset)
            {
                var testable = new TestableSpriteManager();

                testable.ClassUnderTest.Add(new BackgroundImageClass("", 0) { ImageUrl = "image1", ExplicitWidth = image1Width, ExplicitHeight = image1Height, XOffset = new Position { Offset = image1XOffset, PositionMode = PositionMode.Unit} });
                testable.ClassUnderTest.Add(new BackgroundImageClass("", 0) { ImageUrl = "image1", ExplicitWidth = image2Width, ExplicitHeight = image2Height, XOffset = new Position { Offset = image2XOffset, PositionMode = PositionMode.Unit } });

                testable.ClassUnderTest.MockSpriteContainer.Verify(x => x.AddImage(It.IsAny<BackgroundImageClass>()), Times.Exactly(2));
            }

            [Fact]
            public void WillNotAddImageToSpriteContainerIfImageAlreadySprited()
            {
                var testable = new TestableSpriteManager();
                var image = new BackgroundImageClass("", 0) { ImageUrl = "" };
                testable.ClassUnderTest.Add(image);

                testable.ClassUnderTest.Add(image);

                testable.ClassUnderTest.MockSpriteContainer.Verify(x => x.AddImage(image), Times.Exactly(1));
            }

            [Fact]
            public void WillCopySpritesToContainerButAddDistinctCssClass()
            {
                var testable = new TestableSpriteManager();
                var image = new BackgroundImageClass("", 0) { ImageUrl = "url", Selector = ".class" };
                testable.ClassUnderTest.Add(image);
                var image2 = new BackgroundImageClass("", 0) { ImageUrl = "url", Selector = ".class2" };

                testable.ClassUnderTest.Add(image2);

                Assert.Equal(2, testable.ClassUnderTest.SpriteList.Count);
                Assert.Equal(image, testable.ClassUnderTest.SpriteList[0].Value.CssClass);
                Assert.Equal(image2, testable.ClassUnderTest.SpriteList[1].Value.CssClass);
                Assert.Equal(testable.ClassUnderTest.SpriteList[0].Key, testable.ClassUnderTest.SpriteList[1].Key);
            }

            [Fact]
            public void WillNotAddImageToSpriteContainerIfTheRegistryFiltersIt()
            {
                var testable = new TestableSpriteManager();
                var image = new BackgroundImageClass("", 0) { ImageUrl = "", ExplicitWidth = 110};
                testable.ClassUnderTest.ImageExclusionFilter = (x => x.Width > 100);

                testable.ClassUnderTest.Add(image);

                testable.ClassUnderTest.MockSpriteContainer.Verify(x => x.AddImage(image), Times.Never());
            }

            [Fact]
            public void WillSwallowInvalidOperationException()
            {
                var testable = new TestableSpriteManager();
                var image = new BackgroundImageClass("", 0) { ImageUrl = "" };
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.AddImage(image)).Throws(new InvalidOperationException());

                var ex = Record.Exception(() => testable.ClassUnderTest.Add(image));
                
                Assert.Null(ex);
            }

        }

        public class Flush
        {
            [Fact]
            public void WillNotCreateImageWriterIfContainerIsEmpty()
            {
                var testable = new TestableSpriteManager();
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(0);

                testable.ClassUnderTest.Flush();

                testable.Mock<ISpriteStore>().Verify(x => x.SaveSpriteAndReturnUrl(It.IsAny<byte[]>()), Times.Never());
            }

            [Fact]
            public void WillCreateImageWriterWithCorrectDimensions()
            {
                var testable = new TestableSpriteManager();
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Width).Returns(1);
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Height).Returns(1);
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                byte[] bytes = null;
                testable.Mock<IPngOptimizer>()
                    .Setup(x => x.OptimizePng(It.IsAny<byte[]>(), It.IsAny<int>(), false))
                    .Callback<byte[], int, bool>((a, b, c) => bytes = a)
                    .Returns(() => bytes);

                testable.ClassUnderTest.Flush();

                var bitMap = new Bitmap(new MemoryStream(bytes));
                Assert.Equal(1, bitMap.Width);
                Assert.Equal(1, bitMap.Height);
            }

            [Fact]
            public void WillWriteEachImage()
            {
                var testable = new TestableSpriteManager();
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Width).Returns(35);
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Height).Returns(18);
                var images = new List<SpritedImage>
                                 {
                                     new SpritedImage(1, null, TestImages.Image15X17),
                                     new SpritedImage(1, null, TestImages.Image18X18)
                                 };
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.GetEnumerator()).Returns(() => images.GetEnumerator());
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                byte[] bytes = null;
                testable.Mock<IPngOptimizer>()
                    .Setup(x => x.OptimizePng(It.IsAny<byte[]>(), It.IsAny<int>(), false))
                    .Callback<byte[], int, bool>((a, b, c) => bytes = a)
                    .Returns(() => bytes);

                testable.ClassUnderTest.Flush();

                var bitMap = new Bitmap(new MemoryStream(bytes));
                Assert.Equal(TestImages.Image15X17.GraphicsImage(), bitMap.Clone(new Rectangle(0, 0, 15, 17), TestImages.Image15X17.PixelFormat), new BitmapPixelComparer(true));
                Assert.Equal(TestImages.Image18X18.GraphicsImage(), bitMap.Clone(new Rectangle(16, 0, 18, 18), TestImages.Image18X18.PixelFormat), new BitmapPixelComparer(true));
            }

            [Fact]
            public void WillIncrementPositionByWidthOfPreviousImage()
            {
                var testable = new TestableSpriteManager();
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Width).Returns(35);
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Height).Returns(18);
                var images = new List<SpritedImage>
                                 {
                                     new SpritedImage(1, null, TestImages.Image15X17),
                                     new SpritedImage(1, null, TestImages.Image18X18)
                                 };
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.GetEnumerator()).Returns(() => images.GetEnumerator());
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                byte[] bytes = null;
                testable.Mock<IPngOptimizer>()
                    .Setup(x => x.OptimizePng(It.IsAny<byte[]>(), It.IsAny<int>(), false))
                    .Callback<byte[], int, bool>((a, b, c) => bytes = a)
                    .Returns(() => bytes);

                testable.ClassUnderTest.Flush();

                Assert.Equal(16, images[1].Position);
            }

            [Fact]
            public void WillSetPositionToSamePositionOfPreviousDuplicate()
            {
                var testable = new TestableSpriteManager();
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Width).Returns(35);
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Height).Returns(18);
                var metadata = new SpriteManager.ImageMetadata(new BackgroundImageClass("css1",1));
                var metadata2 = new SpriteManager.ImageMetadata(new BackgroundImageClass("css2", 2));
                testable.ClassUnderTest.SpriteList.Add(new KeyValuePair<SpriteManager.ImageMetadata, SpritedImage>(metadata, new SpritedImage(1, null, TestImages.Image15X17) { Metadata = metadata }));
                testable.ClassUnderTest.SpriteList.Add(new KeyValuePair<SpriteManager.ImageMetadata, SpritedImage>(metadata2, new SpritedImage(1, null, TestImages.Image18X18) { Metadata = metadata }));
                testable.ClassUnderTest.SpriteList.Add(new KeyValuePair<SpriteManager.ImageMetadata, SpritedImage>(metadata, new SpritedImage(1, null, TestImages.Image15X17) { Metadata = metadata }));
                var images = new List<SpritedImage>
                                 {
                                     testable.ClassUnderTest.SpriteList[0].Value,
                                     testable.ClassUnderTest.SpriteList[1].Value,
                                 };
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.GetEnumerator()).Returns(() => images.GetEnumerator());
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);

                testable.ClassUnderTest.Flush();

                Assert.Equal(0, testable.ClassUnderTest.SpriteList[0].Value.Position);
                Assert.Equal(16, testable.ClassUnderTest.SpriteList[1].Value.Position);
                Assert.Equal(0, testable.ClassUnderTest.SpriteList[2].Value.Position);
            }

            [Fact]
            public void WillSaveWriterToContainerUrlUsingPngMimeType()
            {
                var testable = new TestableSpriteManager();
                byte[] bytes = null;
                testable.Mock<IPngOptimizer>().Setup(x => x.OptimizePng(It.IsAny<byte[]>(), It.IsAny<int>(), false))
                    .Callback<byte[], int, bool>((a, b, c) => bytes = a)
                    .Returns(() => bytes);
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);

                testable.ClassUnderTest.Flush();

                var bitMap = new Bitmap(new MemoryStream(bytes));
                Assert.Equal(ImageFormat.Png, bitMap.RawFormat);
            }

            [Fact]
            public void WillResetSpriteContainerAfterFlush()
            {
                var testable = new TestableSpriteManager();
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Width).Returns(20);

                testable.ClassUnderTest.Flush();

                Assert.Equal(0, testable.ClassUnderTest.SpriteContainer.Width);
            }

            [Fact]
            public void WillAddUrlsToSpritesUponFlush()
            {
                var testable = new TestableSpriteManager();
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                var sprites = new List<SpritedImage> { new SpritedImage(1, null, TestImages.Image15X17), new SpritedImage(1, null, TestImages.Image18X18) };
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.GetEnumerator()).Returns(
                    () => sprites.GetEnumerator());

                testable.ClassUnderTest.Flush();

                Assert.Equal("url", sprites[0].Url);
                Assert.Equal("url", sprites[1].Url);
            }

            [Fact]
            public void WillOptimizeImageIfOptimizationIsEnabled()
            {
                var testable = new TestableSpriteManager();
                testable.Settings.ImageOptimizationDisabled = false;
                testable.Settings.ImageOptimizationCompressionLevel = 2;
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                var optimizedBytes = new byte[] {5, 5, 5, 5, 5};
                testable.Mock<IPngOptimizer>().Setup(x => x.OptimizePng(It.IsAny<byte[]>(), 2, false)).Returns(optimizedBytes).Verifiable();

                testable.ClassUnderTest.Flush();

                testable.Mock<IPngOptimizer>().VerifyAll();
            }

            [Fact]
            public void WillNotOptimizeImageIfOptimizationIsDisabled()
            {
                var testable = new TestableSpriteManager();
                testable.Settings.ImageOptimizationDisabled = true;
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                var optimizedBytes = new byte[0];
                testable.Mock<IPngOptimizer>().Setup(x => x.OptimizePng(It.IsAny<byte[]>(), It.IsAny<int>(), false)).
                    Callback<byte[], int, bool>((a, b, c) => optimizedBytes = a).Returns(() => optimizedBytes);

                testable.ClassUnderTest.Flush();

                Assert.Empty(optimizedBytes);
            }

            [Fact]
            public void WillNotOptimizeImageIfNotInFullTrust()
            {
                var testable = new TestableSpriteManager();
                testable.Settings.IsFullTrust = false;
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                var optimizedBytes = new byte[0];
                testable.Mock<IPngOptimizer>().Setup(x => x.OptimizePng(It.IsAny<byte[]>(), It.IsAny<int>(), false)).
                    Callback<byte[], int, bool>((a, b, c) => optimizedBytes = a).Returns(() => optimizedBytes);

                testable.ClassUnderTest.Flush();

                Assert.Empty(optimizedBytes);
            }

            [Theory,
            InlineData(2),
            InlineData(3)]
            public void WillPassConfiguredCompressionLevelToOptimizer(int expectedCompression)
            {
                var testable = new TestableSpriteManager();
                int compression = 0;
                byte[] bytes = null;
                testable.Settings.ImageOptimizationDisabled = false;
                testable.Settings.ImageOptimizationCompressionLevel = expectedCompression;
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                testable.Mock<IPngOptimizer>().Setup(x => x.OptimizePng(It.IsAny<byte[]>(), It.IsAny<int>(), false)).
                    Callback<byte[], int, bool>((a, b, c) => { compression = b;
                                                                 bytes = a;
                    }).Returns(() => bytes);

                testable.ClassUnderTest.Flush();

                Assert.Equal(expectedCompression, compression);
            }

            [Theory,
            InlineData(true),
            InlineData(false)]
            public void WillPassConfiguredQuantiaztionEnablementToOptimizer(bool expectedToBeDisabled)
            {
                var testable = new TestableSpriteManager();
                bool isQuantizationDisabled = false;
                byte[] bytes = null;
                testable.Settings.ImageOptimizationDisabled = false;
                testable.Settings.ImageQuantizationDisabled = expectedToBeDisabled;
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                testable.Mock<IPngOptimizer>().Setup(
                    x => x.OptimizePng(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<bool>())).Callback
                    <byte[], int, bool>((a, b, c) =>
                                            {
                                                isQuantizationDisabled=c;
                                                bytes=a;
                                            }).Returns(() => bytes);

                testable.ClassUnderTest.Flush();

                Assert.Equal(expectedToBeDisabled, isQuantizationDisabled);
            }

            [Fact]
            public void WillUseUnoptimizedBytesIfOptimizationFails()
            {
                var testable = new TestableSpriteManager();
                byte[] originalBytes = null;
                byte[] optimizedBytes = null;
                var images = new List<SpritedImage> { new SpritedImage(1, null, TestImages.Image15X17), new SpritedImage(1, null, TestImages.Image18X18) };
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.GetEnumerator()).Returns(() => images.GetEnumerator());
                testable.Settings.ImageOptimizationDisabled = false;
                testable.Settings.ImageOptimizationCompressionLevel = 2;
                testable.Mock<ISpriteStore>().Setup(x => x.SaveSpriteAndReturnUrl(It.IsAny<byte[]>())).Callback<byte[]>(bytesToSave => optimizedBytes = bytesToSave).Returns("url");
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                testable.Mock<IPngOptimizer>()
                    .Setup(x => x.OptimizePng(It.IsAny<byte[]>(), 2, false))
                    .Callback<byte[], int, bool>((a, b, c) => originalBytes = a)
                    .Throws(new OptimizationException(""));

                testable.ClassUnderTest.Flush();

                Assert.Equal(optimizedBytes, originalBytes);
            }

            [Fact]
            public void WillPassOptimizationErrorToErrorHandler()
            {
                var testable = new TestableSpriteManager();
                var exception = new OptimizationException("Appropriately friendly error message");
                testable.Settings.ImageOptimizationDisabled = false;
                testable.Settings.ImageOptimizationCompressionLevel = 2;
                testable.ClassUnderTest.MockSpriteContainer.Setup(x => x.Size).Returns(1);
                testable.Mock<IPngOptimizer>().Setup(x => x.OptimizePng(It.IsAny<byte[]>(), 2, false)).Throws(exception);

                testable.ClassUnderTest.Flush();

                Assert.Equal(testable.ClassUnderTest.Errors[0], exception);
                Assert.Equal(testable.ClassUnderTest.Errors[0].Message, "Appropriately friendly error message");
            }

        }

        public class Enumerator
        {
            [Fact]
            public void WillReturnAllImages()
            {
                var testable = new TestableSpriteManager();
                testable.ClassUnderTest.Add(new BackgroundImageClass("class1", 0) {ExplicitHeight = 1});
                testable.ClassUnderTest.Add(new BackgroundImageClass("class2", 0) {ExplicitHeight = 2});

                var results = testable.ClassUnderTest.ToArray();

                Assert.Equal(2, results.Length);
                Assert.True(results.Any(x => x.CssClass.OriginalClassString == "class1"));
                Assert.True(results.Any(x => x.CssClass.OriginalClassString == "class2"));
            }
        }

    }
}