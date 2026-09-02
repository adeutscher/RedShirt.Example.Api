using System.Linq.Expressions;

namespace RedShirt.Example.Api.Upload.Implementation.Predicates;

internal sealed class PredicateBuilder<T>
{
    private Expression<Func<T, bool>>? _predicate;

    private static Expression<Func<T, bool>> Combine(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        var parameter = Expression.Parameter(typeof(T), "entity");
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        return Expression.Lambda<Func<T, bool>>(merge(leftBody, rightBody), parameter);
    }

    private static Expression ReplaceParameter(Expression body, ParameterExpression source, ParameterExpression target)
    {
        return new ParameterReplacer(source, target).Visit(body);
    }

    public PredicateBuilder<T> And(Expression<Func<T, bool>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        _predicate = _predicate is null ? expression : Combine(_predicate, expression, Expression.AndAlso);
        return this;
    }

    public Expression<Func<T, bool>> Build()
    {
        return _predicate ?? (static _ => true);
    }

    private sealed class ParameterReplacer(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == source ? target : base.VisitParameter(node);
        }
    }
}
