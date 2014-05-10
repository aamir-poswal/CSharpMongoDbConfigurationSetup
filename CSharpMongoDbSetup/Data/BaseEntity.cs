using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.GeoJsonObjectModel;
using MongoRepository;
using Newtonsoft.Json;
using log4net.Config;

namespace Data
{
    /// <summary>
    /// Represents the base entity for all persisted entity types.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity this base entity is wrapped by</typeparam>
    [Serializable, JsonObject(MemberSerialization.OptOut)]
    [BsonIgnoreExtraElements(false, Inherited = true)]
    public abstract class BaseEntity<TEntity> : Entity where TEntity : BaseEntity<TEntity>, new()
    {

        #region Public Methods
        /// <summary>
        /// Upserts (Inserts or Updates) this entity to the database and if it does not already exist,
        /// sets its new Id property as well.
        /// </summary>
        /// <exception cref="System.OperationCanceledException">If the cancelable event args <c>Canceled</c>
        /// property is set to true.</exception>
        public virtual TEntity Save()
        {
            if (OnSaving())
            {
                TEntity updated = Repository.Update((TEntity)this);
                OnSaved();
                return updated;
            }
            throw new OperationCanceledException();
        }//Save

        /// <summary>
        /// Deletes this entity instance from the database.
        /// </summary>
        /// <exception cref="System.OperationCanceledException">If the cancelable event args <c>Canceled</c>
        /// property is set to true.</exception>
        public virtual void Delete()
        {
            if (OnDeleting())
            {
                Repository.Delete((TEntity)this);
                OnDeleted();
            }
            else
                throw new OperationCanceledException();

        }//Delete

        #endregion

        #region Public Events

        /// <summary>
        /// Event is called before this entity of type TEntity is saved. This is useful
        /// for additional validation, etc. and may be canceled.
        /// </summary>
        public event Action<object, CancelableEntityEventArgs<TEntity>> Saving;

        /// <summary>
        /// Event is called after an entity has been saved, passing the newly saved entity.
        /// </summary>
        public event Action<object, EntityEventArgs<TEntity>> Saved;

        /// <summary>
        /// Event is called before this entity of type TEntity is deleted. This is useful
        /// for additional validation, etc. and may be canceled.
        /// </summary>
        public event Action<object, CancelableEntityEventArgs<TEntity>> Deleting;

        /// <summary>
        /// Event is called after an entity has been saved, passing the recently deleted entity.
        /// </summary>
        public event Action<object, EntityEventArgs<TEntity>> Deleted;

        #endregion

        #region Protected Handlers

        /// <summary>
        /// Called before saving
        /// </summary>
        /// <returns>A value indicating whether or not the save should proceed (not canceled).</returns>
        protected virtual bool OnSaving()
        {
            CancelableEntityEventArgs<TEntity> args = new CancelableEntityEventArgs<TEntity>((TEntity)this);
            if (Saving != null)
                Saving(this, args);
            return !args.Canceled;
        }//OnSaving

        /// <summary>
        /// Called when saved
        /// </summary>
        protected virtual void OnSaved()
        {
            if (Saved != null) Saved(this, new EntityEventArgs<TEntity>((TEntity)this));
        }//OnSaved

        /// <summary>
        /// Called before deleting
        /// </summary>
        /// <returns>A value indicating whether or not the delete should proceed (not canceled).</returns>
        protected virtual bool OnDeleting()
        {
            CancelableEntityEventArgs<TEntity> args = new CancelableEntityEventArgs<TEntity>((TEntity)this);
            if (Deleting != null)
                Deleting(this, args);
            return !args.Canceled;
        }//OnDeleting

        /// <summary>
        /// Called when deleted
        /// </summary>
        protected virtual void OnDeleted()
        {
            if (Deleted != null) Deleted(this, new EntityEventArgs<TEntity>((TEntity)this));
        }//OnDeleted

        #endregion

        #region Protected Methods

        /// <summary>
        /// The protected reference instance dictionary used for caching entity references.
        /// </summary>
        [BsonIgnore, JsonIgnore]
        protected Dictionary<string, object> _refs = new Dictionary<string, object>();

        /// <summary>
        /// Gets the reference value for an Id based on the reference type passed into TRef.
        /// </summary>
        /// <typeparam name="TRef">The entity type the reference Id is referencing</typeparam>
        /// <param name="refId">The Id of the reference type to get by</param>
        /// <returns>An instance of the reference by Id or <c>null</c> if the reference is not found or Id is null.</returns>
        protected virtual TRef GetReferenceValue<TRef>(string refId) where TRef : BaseEntity<TRef>, new()
        {
            string key = typeof(TRef).Name;

            TRef existingVal = _refs.ContainsKey(key) ? _refs[key] as TRef : null;
            if (!string.IsNullOrWhiteSpace(refId) && (existingVal == null || existingVal.Id != refId))
                existingVal = typeof(TRef).GetMethod("GetById", BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public).Invoke(null, new object[] { refId }) as TRef;
            else if (string.IsNullOrWhiteSpace(refId))
                existingVal = null;

            _refs[key] = existingVal;

            return existingVal;
        }//GetReferenceValue

        /// <summary>
        /// Sets the reference value for an Id based on the reference type passed into TRef, but also returns
        /// the Id of the set entity for setting the actual Id property value back in the parent entity.
        /// </summary>
        /// <typeparam name="TRef">The type value to be passed in to set</typeparam>
        /// <param name="value">The instance of the type value or <c>null</c> if being cleared out</param>
        /// <returns>The Id of the passed in value or <c>null</c> if no entity passed in</returns>
        protected virtual string SetReferenceValue<TRef>(TRef value) where TRef : BaseEntity<TRef>, new()
        {
            if (value == null)
                return null;

            string key = typeof(TRef).Name;
            _refs[key] = value;

            return value.Id;
        }//SetReferenceValue

        #endregion

        #region Repository

        [BsonIgnore, JsonIgnore]
        public static readonly string TypeName = typeof(TEntity).Name;

        [BsonIgnore, JsonIgnore]
        private static object _repoSync = new object();
        [BsonIgnore, JsonIgnore]
        private static MongoRepository<TEntity> _myRepository = null;
        /// <summary>
        /// Gets the repository used for manipulating this instance.
        /// </summary>
        [BsonIgnore, JsonIgnore]
        public static MongoRepository<TEntity> Repository
        {
            get
            {
                if (_myRepository != null)
                    return _myRepository;

                lock (_repoSync)
                {
                    if (_myRepository != null)
                        return _myRepository;

                    var myRepo = typeof(TEntity)
                        .GetCustomAttributes(typeof(RepositoryAttribute), true)
                        .OfType<RepositoryAttribute>()
                        .FirstOrDefault();

                    string cName = (myRepo == null || string.IsNullOrWhiteSpace(myRepo.ConnectionStringName)) ? "MongoServerSettings" : myRepo.ConnectionStringName;
                    var connString = ConfigurationManager.ConnectionStrings[cName];
                    if (connString == null)
                        throw new ApplicationException(string.Format("MongoDB connection string '{0}' is missing from configuration", cName));
                    _myRepository = new MongoRepository<TEntity>(connString.ConnectionString, TypeName);
                }

                return _myRepository;
            }
        }//end: Repository

        /// <summary>
        /// Gets the entity by its Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>The entity found by the Id or <c>null</c> if no entity was found by that id.</returns>
        public static TEntity GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return default(TEntity);

            return Repository.GetById(id.ToLowerInvariant());

        }

        /// <summary>
        /// Gets a queryable version of this entity type's repository for manual searching.
        /// </summary>
        /// <returns>A queryable version of this entity type's repository for manual searching.</returns>
        public static IQueryable<TEntity> AsQueryable()
        {
            return Repository.AsQueryable();
        }

        /// <summary>
        /// Counts the total entities in the repository.
        /// </summary>
        /// <returns>Count of entities in the collection.</returns>
        public static long Count()
        {
            return Repository.Count();
        }

        /// <summary>
        /// Deletes an entity from the repository by its id.
        /// </summary>
        /// <param name="id">The entity's id.</param>
        /// <exception cref="201"></exception>
        public static void Delete(string id)
        {
            var entity = GetById(id);
            if (entity != null)
                entity.Delete();
        }

        /// <summary>
        /// Deletes all entities in the repository.
        /// </summary>
        public static void DeleteAll()
        {
            Repository.DeleteAll();
        }

        /// <summary>
        /// Checks if the entity exists for given predicate.
        /// </summary>
        /// <param name="predicate">The expression.</param>
        /// <returns>True when an entity matching the predicate exists, false otherwise.</returns>
        public static bool Exists(Expression<Func<TEntity, bool>> predicate)
        {
            return Repository.Exists(predicate);
        }

        #endregion
        
    }

    #region EntityEventArgs

    /// <summary>
    /// Represents an entity event argument as passed to entity events AFTER they occur.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity that this entity event argument represents or is acted upon.</typeparam>
    public class EntityEventArgs<TEntity> : EventArgs where TEntity : BaseEntity<TEntity>, new()
    {
        /// <summary>
        /// Initializes a new instance of the CancelableEntityEventArgs class passing the entity value.
        /// </summary>
        /// <param name="entity">The entity value as it exists after the event occurred</param>
        public EntityEventArgs(TEntity entity)
        {
            this.Entity = entity;
        }//ctor

        /// <summary>
        /// Gets the Entity that this event was triggered from or the entity that was impacted by the event.
        /// </summary>
        public TEntity Entity { get; private set; }
    }//EntityEventArgs<TEntity>

    /// <summary>
    /// Represents a cancelable entity event argument as passed to entity events BEFORE they occur.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity that this entity event argument represents or will be acted upon.</typeparam>
    public class CancelableEntityEventArgs<TEntity> : EntityEventArgs<TEntity> where TEntity : BaseEntity<TEntity>, new()
    {
        /// <summary>
        /// Initializes a new instance of the CancelableEntityEventArgs class passing the entity value.
        /// </summary>
        /// <param name="entity">The entity value as it exists before the event occurrs</param>
        public CancelableEntityEventArgs(TEntity entity)
            : base(entity)
        {
            this.Canceled = false;
        }//ctor

        /// <summary>
        /// Gets or sets a value indicating whether the event should be canceled.
        /// </summary>
        public bool Canceled { get; set; }
    }//CancelableEntityEventArgs<TEntity>

    #endregion
}
