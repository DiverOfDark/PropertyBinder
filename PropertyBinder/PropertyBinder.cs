﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using PropertyBinder.Engine;
using PropertyBinder.Helpers;
using PropertyBinder.Visitors;

namespace PropertyBinder
{
    public sealed class PropertyBinder<TContext>
        where TContext : class
    {
        private readonly IDictionary<string, List<Action<TContext>>> _keyedActions;
        private readonly IBindingNode<TContext, TContext> _rootNode;
        private readonly UniqueActionCollection<TContext> _attachActions;

        private PropertyBinder(IBindingNode<TContext, TContext> rootNode, IDictionary<string, List<Action<TContext>>> keyedActions, UniqueActionCollection<TContext> attachActions)
        {
            _rootNode = rootNode;
            _keyedActions = keyedActions;
            _attachActions = attachActions;
        }

        public PropertyBinder()
            : this(new BindingNode<TContext, TContext, TContext>(x => x), new Dictionary<string, List<Action<TContext>>>(), new UniqueActionCollection<TContext>())
        {
        }

        public PropertyBinder<TNewContext> Clone<TNewContext>()
            where TNewContext : class, TContext
        {
            return new PropertyBinder<TNewContext>(_rootNode.CloneForDerivedType<TNewContext>(), _keyedActions.ToDictionary(x => x.Key, x => new List<Action<TNewContext>>(x.Value)), _attachActions.Clone<TNewContext>());
        }

        public PropertyBinder<TContext> Clone()
        {
            return Clone<TContext>();
        }

        internal void AddRule(Action<TContext> bindingAction, string key, bool runOnAttach, bool canOverride, IEnumerable<LambdaExpression> triggerExpressions)
        {
            if (!string.IsNullOrEmpty(key))
            {
                List<Action<TContext>> existingActions;
                if (!_keyedActions.TryGetValue(key, out existingActions))
                {
                    _keyedActions.Add(key, existingActions = new List<Action<TContext>>());
                }

                if (canOverride)
                {
                    foreach (var action in existingActions)
                    {
                        _attachActions.Remove(action);
                        _rootNode.RemoveActionCascade(action);
                    }
                }

                existingActions.Add(bindingAction);
            }

            if (runOnAttach)
            {
                _attachActions.Add(bindingAction);
            }

            foreach (var expr in triggerExpressions)
            {
                new BindingExpressionVisitor<TContext>(_rootNode, expr.Parameters[0].Type, bindingAction).Visit(expr);
            }
        }

        internal void RemoveRule(string key)
        {
            List<Action<TContext>> existingActions;
            if (_keyedActions.TryGetValue(key, out existingActions))
            {
                foreach (var action in existingActions)
                {
                    _attachActions.Remove(action);
                    _rootNode.RemoveActionCascade(action);
                }

                _keyedActions.Remove(key);
            }
        }

        public IDisposable Attach(TContext context)
        {
            if (_attachActions != null)
            {
                _attachActions.Execute(context);
            }

            var watcher = _rootNode.CreateWatcher(context);
            watcher.Attach(context);
            return watcher;
        }
    }
}