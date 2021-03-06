using System;
using System.Threading.Tasks;
using Xunit;
using TestData = CSharpMath.Rendering.Tests.TestRenderingMathData;

namespace CSharpMath.Ios.Tests {
  public class Tests {
    /// <summary>Maximum percentage change from expected file size to actual file size / 100</summary>
    /// <remarks>Same idea as CSharpMath.Rendering.Tests.TestRendering.FileSizeTolerance.</remarks>
    const double FileSizeTolerance = 1.68; // This is too large... (should be <0.01) We need to devise an alternative test mechanism
    static readonly Func<string, System.IO.Stream> GetManifestResourceStream =
      System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream;
    async Task Test(string directory, Action<Apple.AppleMathView> init, string file, string latex) {
      var source = new TaskCompletionSource<UIKit.UIImage>();
      Foundation.NSRunLoop.Main.BeginInvokeOnMainThread(() => {
        try {
          using var v = IosMathLabels.MathView(latex, 50);
          init(v);
          var size = v.SizeThatFits(default);
          // BeginImageContext does not support zero width/height. GetCurrentContext will return null.
          if (size.Width < 1) size.Width = 1;
          if (size.Height < 1) size.Height = 1;
          v.Frame = new CoreGraphics.CGRect(default, size);
          UIKit.UIGraphics.BeginImageContext(size);
          var context = UIKit.UIGraphics.GetCurrentContext();
          context.ScaleCTM(1, -1);
          context.TranslateCTM(0, -size.Height);
          if (!v.DrawViewHierarchy(v.Frame, true))
            throw new Exception(nameof(v.DrawViewHierarchy) + " has failed.");
          source.SetResult(UIKit.UIGraphics.GetImageFromCurrentImageContext());
          UIKit.UIGraphics.EndImageContext();
        } catch (Exception e) {
          source.SetException(e);
        }
      });

      using var expected = GetManifestResourceStream($"CSharpMath.Ios.Tests.{directory}.{file}.png");
      using var actual = (await source.Task).AsPNG().AsStream();

      // Save the generated image
      var documents = Foundation.NSSearchPath.GetDirectories(Foundation.NSSearchPathDirectory.DocumentDirectory, Foundation.NSSearchPathDomain.User, true)[0];
      var dir = new System.IO.DirectoryInfo(documents).CreateSubdirectory(directory).FullName;
      using var save = System.IO.File.Create(System.IO.Path.Combine(dir, $"{file}.ios.png"));
      actual.CopyTo(save);

      switch (file) {
        // The following are produced by inherently different implementations, so they are not comparable
        case nameof(TestData.Cyrillic):
        case nameof(TestData.ErrorInvalidColor):
        case nameof(TestData.ErrorMissingArgument):
        case nameof(TestData.ErrorMissingBrace):
          break;
        default:
          Assert.InRange(actual.Length, expected.Length * (1 - FileSizeTolerance), expected.Length * (1 + FileSizeTolerance));
          break;
      }
    }
    [Theory]
    [ClassData(typeof(TestData))]
    public Task MathInline(string file, string latex) =>
      Test(nameof(MathInline), v => v.LineStyle = Atom.LineStyle.Text, file, latex);
    [Theory]
    [ClassData(typeof(TestData))]
    public Task MathDisplay(string file, string latex) =>
      Test(nameof(MathDisplay), v => { }, file, latex);
  }
}
