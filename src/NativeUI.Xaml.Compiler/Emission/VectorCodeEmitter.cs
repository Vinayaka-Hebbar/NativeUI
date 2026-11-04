using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NativeUI.Media;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Builds typed geometry and SVG expressions using the same syntax tree model as the XAML emitter.
/// </summary>
internal static class VectorCodeEmitter
{
    /// <summary>
    /// Builds immutable figures and segments from normalized path commands.
    /// </summary>
    public static ObjectCreationExpressionSyntax Geometry(IReadOnlyList<PathCommand> commands)
    {
        List<ExpressionSyntax> figures = [];
        List<ExpressionSyntax> segments = [];
        ExpressionSyntax startPoint = CreatePoint(0, 0);
        bool hasOpenFigure = false;

        void FinishFigure(bool isClosed)
        {
            figures.Add(CreateObject("PathFigure").WithInitializer(
                InitializerExpression(SyntaxKind.ObjectInitializerExpression, SeparatedList<ExpressionSyntax>(
                [
                    Assign("StartPoint", startPoint),
                    Assign("IsClosed", Boolean(isClosed)),
                    Assign("Segments", Collection(segments))
                ]))));
            segments.Clear();
        }

        foreach (PathCommand command in commands)
        {
            double[] values = command.Values;
            switch (command.Kind)
            {
                case 'M':
                    if (hasOpenFigure)
                    {
                        FinishFigure(false);
                    }

                    startPoint = CreatePoint(values[0], values[1]);
                    hasOpenFigure = true;
                    break;
                case 'Z':
                    FinishFigure(true);
                    hasOpenFigure = false;
                    break;
                default:
                    hasOpenFigure = true;
                    segments.Add(CreateSegment(command));
                    break;
            }
        }

        if (hasOpenFigure)
        {
            FinishFigure(false);
        }

        return CreateObject("PathGeometry", Collection(figures));
    }

    /// <summary>Builds a typed immutable SVG definition without runtime attribute parsing.</summary>
    public static ObjectCreationExpressionSyntax Node(SvgNodeData node)
    {
        List<ExpressionSyntax> properties = [];
        if (node.Commands is not null) properties.Add(Assign("Geometry", Geometry(node.Commands)));
        if (node.Text.Length > 0) properties.Add(Assign("Text", Text(node.Text)));
        foreach (SvgPropertyData property in node.Properties) properties.Add(Assign(property.Name, SvgValue(property)));
        if (node.Children.Length > 0) properties.Add(Assign("Children", Collection(node.Children.Select(Node))));
        return CreateObject("SvgNode", Member("SvgNodeKind", SvgValueParser.NodeKind(node.Name)))
            .WithInitializer(InitializerExpression(SyntaxKind.ObjectInitializerExpression, SeparatedList(properties)));
    }

    private static ExpressionSyntax SvgValue(SvgPropertyData value)
    {
        double[] numbers = value.Numbers;
        return value.Kind switch
        {
            SvgValueKind.Text => Text(value.Text ?? string.Empty),
            SvgValueKind.Boolean => Boolean(numbers[0] == 1),
            SvgValueKind.Number when value.Name == "FontWeight" => CastExpression(IdentifierName("FontWeight"), Number(numbers[0])),
            SvgValueKind.Number => Number(numbers[0]),
            SvgValueKind.Length => CreateObject("SvgLength", Number(numbers[0]), Boolean(numbers[1] == 1)),
            SvgValueKind.Rect => CreateObject("Rect", numbers.Select(Number).ToArray()),
            SvgValueKind.Matrix => CreateObject("Matrix3x2", numbers.Select(number => (ExpressionSyntax)LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((float)number))).ToArray()),
            SvgValueKind.Numbers => Collection(numbers.Select(Number)),
            SvgValueKind.Color => SvgColor(value),
            SvgValueKind.Paint => value.Type switch
            {
                "None" => Member("SvgNoBrush", "Instance"),
                "CurrentColor" => Member("SvgCurrentColorBrush", "Instance"),
                "Reference" => CreateObject("SvgReferenceBrush", Text(value.Text ?? string.Empty)),
                _ => CreateObject("SolidColorBrush", SvgColor(value))
            },
            SvgValueKind.Enum => Member(value.Type ?? throw new FormatException("Missing SVG enum type."), value.Text ?? string.Empty),
            SvgValueKind.Aspect => CreateObject("SvgAspectRatio", Member("SvgAlignment", value.Text ?? "XMidYMid"),
                Member("Stretch", value.Text == "None" ? "Fill" : numbers[0] == 1 ? "UniformToFill" : "Uniform")),
            _ => throw new FormatException("Unsupported parsed SVG value.")
        };
    }

    private static ExpressionSyntax SvgColor(SvgPropertyData value)
    {
        if (value.Text is string text && XamlValueConverters.TryConvert(XamlPropertyKind.Color, text, out ExpressionSyntax? color) && color is not null)
        {
            return color;
        }
        return InvocationExpression(Member("Color", "FromSrgb"))
            .WithArgumentList(ArgumentList(SeparatedList(value.Numbers.Select(number => Argument(Number(number))))));
    }

    private static MemberAccessExpressionSyntax Member(string type, string member) =>
        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(type), IdentifierName(member));

    private static ObjectCreationExpressionSyntax CreateSegment(PathCommand command)
    {
        double[] values = command.Values;
        return command.Kind switch
        {
            'L' => CreateObject("LineSegment", CreatePoint(values[0], values[1])),
            'Q' => CreateObject("QuadraticBezierSegment").WithInitializer(
                InitializerExpression(SyntaxKind.ObjectInitializerExpression, SeparatedList<ExpressionSyntax>(
                [
                    Assign("ControlPoint", CreatePoint(values[0], values[1])),
                    Assign("Point", CreatePoint(values[2], values[3]))
                ]))),
            'C' => CreateObject("BezierSegment").WithInitializer(
                InitializerExpression(SyntaxKind.ObjectInitializerExpression, SeparatedList<ExpressionSyntax>(
                [
                    Assign("ControlPoint1", CreatePoint(values[0], values[1])),
                    Assign("ControlPoint2", CreatePoint(values[2], values[3])),
                    Assign("Point", CreatePoint(values[4], values[5]))
                ]))),
            'A' => CreateObject("ArcSegment").WithInitializer(
                InitializerExpression(SyntaxKind.ObjectInitializerExpression, SeparatedList<ExpressionSyntax>(
                [
                    Assign("Size", CreateObject("Size", Number(values[0]), Number(values[1]))),
                    Assign("RotationAngle", Number(values[2])),
                    Assign("IsLargeArc", Boolean(values[3] == 1)),
                    Assign("SweepDirection", MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("SweepDirection"),
                        IdentifierName(values[4] == 1 ? "Clockwise" : "CounterClockwise"))),
                    Assign("Point", CreatePoint(values[5], values[6]))
                ]))),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.Kind, "Expected a normalized segment command.")
        };
    }

    private static ObjectCreationExpressionSyntax CreatePoint(double x, double y)
    {
        return CreateObject("Point", Number(x), Number(y));
    }

    private static ObjectCreationExpressionSyntax CreateObject(string typeName, params ExpressionSyntax[] arguments)
    {
        return ObjectCreationExpression(IdentifierName(typeName))
            .WithArgumentList(ArgumentList(SeparatedList(arguments.Select(Argument))));
    }

    private static AssignmentExpressionSyntax Assign(string propertyName, ExpressionSyntax value)
    {
        return AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(propertyName), value);
    }

    private static CollectionExpressionSyntax Collection(IEnumerable<ExpressionSyntax> values)
    {
        return CollectionExpression(SeparatedList<CollectionElementSyntax>(values.Select(ExpressionElement)));
    }

    private static LiteralExpressionSyntax Boolean(bool value)
    {
        return LiteralExpression(value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
    }

    private static LiteralExpressionSyntax Text(string value)
    {
        return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(value));
    }

    private static ExpressionSyntax Number(double value)
    {
        return value < 0
            ? PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(-value)))
            : LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));
    }
}
