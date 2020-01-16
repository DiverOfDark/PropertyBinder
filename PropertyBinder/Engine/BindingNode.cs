﻿using System;
using System.Collections.Generic;
using System.Linq;
using PropertyBinder.Helpers;

namespace PropertyBinder.Engine
{
    internal class BindingNode<TParent, TNode> : IBindingNode<TParent>
    {
        protected readonly Func<TParent, TNode> _targetSelector;
        protected readonly IDictionary<string, List<int>> _bindingActions;
        protected IDictionary<string, IBindingNode<TNode>> _subNodes;
        protected ICollectionBindingNode<TNode> _collectionNode;

        protected BindingNode(Func<TParent, TNode> targetSelector, IDictionary<string, IBindingNode<TNode>> subNodes, IDictionary<string, List<int>> bindingActions, ICollectionBindingNode<TNode> collectionNode)
        {
            _targetSelector = targetSelector;
            _subNodes = subNodes;
            _bindingActions = bindingActions;
            _collectionNode = collectionNode;
        }

        public BindingNode(Func<TParent, TNode> targetSelector)
            : this(targetSelector, null, new Dictionary<string, List<int>>(), null)
        {
            _targetSelector = targetSelector;
        }

        public bool HasBindingActions
        {
            get
            {
                if (_bindingActions.Count != 0)
                {
                    return true;
                }

                if (_subNodes != null && _subNodes.Values.Any(x => x.HasBindingActions))
                {
                    return true;
                }

                if (_collectionNode != null && _collectionNode.HasBindingActions)
                {
                    return true;
                }

                return false;
            }
        }

        public IBindingNode GetSubNode(BindableMember member)
        {
            if (_subNodes == null)
            {
                _subNodes = new Dictionary<string, IBindingNode<TNode>>();
            }

            IBindingNode<TNode> node;
            if (!_subNodes.TryGetValue(member.Name, out node))
            {
                var selector = member.CreateSelector(typeof(TNode));
                node = (IBindingNode<TNode>)Activator.CreateInstance(typeof(BindingNode<,>).MakeGenericType(typeof(TNode), selector.Method.ReturnType), selector);
                _subNodes.Add(member.Name, node);
            }

            return node;
        }

        public ICollectionBindingNode GetCollectionNode(Type itemType)
        {
            return _collectionNode ?? (_collectionNode = (ICollectionBindingNode<TNode>) Activator.CreateInstance(typeof (CollectionBindingNode<,>).MakeGenericType(typeof (TNode), itemType)));
        }

        public void AddAction(string memberName, int actionIndex)
        {
            List<int> currentAction;
            if (!_bindingActions.TryGetValue(memberName, out currentAction))
            {
                _bindingActions[memberName] = currentAction = new List<int>();
            }
            currentAction.Add(actionIndex);
        }

        public IObjectWatcher<TParent> CreateWatcher(Func<ICollection<int>, Binding[]> bindingsFactory)
        {
            return new ObjectWatcher<TParent, TNode>(
                _targetSelector,
                CreateSubWatchers(bindingsFactory),
                _bindingActions.ToReadOnlyDictionary(x => x.Key, x => bindingsFactory(x.Value)),
                _collectionNode?.CreateWatcher(bindingsFactory));
        }

        public IBindingNode<TParent> Clone()
        {
            return new BindingNode<TParent, TNode>(
                _targetSelector,
                _subNodes?.ToDictionary(x => x.Key, x => x.Value.Clone()),
                _bindingActions.ToDictionary(x => x.Key, x => new List<int>(x.Value)),
                _collectionNode?.Clone());
        }

        public virtual IBindingNode<TNewParent> CloneForDerivedParentType<TNewParent>()
            where TNewParent : TParent
        {
            return new BindingNode<TNewParent, TNode>(
                x => _targetSelector(x),
                _subNodes?.ToDictionary(x => x.Key, x => x.Value.Clone()),
                _bindingActions.ToDictionary(x => x.Key, x => new List<int>(x.Value)),
                _collectionNode?.Clone());
        }

        private IReadOnlyDictionary<string, IObjectWatcher<TNode>> CreateSubWatchers(Func<ICollection<int>, Binding[]> bindingsFactory)
        {
            return _subNodes?.ToReadOnlyDictionary(x => x.Key, x => x.Value.CreateWatcher(bindingsFactory));
        }
    }

    internal sealed class BindingNodeRoot<TContext> : BindingNode<TContext, TContext>
    {
        private BindingNodeRoot(IDictionary<string, IBindingNode<TContext>> subNodes, IDictionary<string, List<int>> bindingActions, ICollectionBindingNode<TContext> collectionNode)
            : base(_ => _, subNodes, bindingActions, collectionNode)
        {
        }

        public BindingNodeRoot()
            : base(_ => _)
        {
        }

        public override IBindingNode<TNewParent> CloneForDerivedParentType<TNewParent>()
        {
            return new BindingNodeRoot<TNewParent>(
                _subNodes?.ToDictionary(x => x.Key, x => x.Value.CloneForDerivedParentType<TNewParent>()),
                _bindingActions.ToDictionary(x => x.Key, x => new List<int>(x.Value)),
                _collectionNode?.CloneForDerivedParentType<TNewParent>());
        }
    }
}