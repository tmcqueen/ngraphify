using System;
using System.Collections.Generic;

namespace SampleApp.Models
{
    public interface IRepository<T>
    {
        T GetById(int id);
        void Save(T entity);
    }

    public class UserRepository : IRepository<User>
    {
        private readonly DbContext _context;

        public UserRepository(DbContext context)
        {
            _context = context;
        }

        public User GetById(int id)
        {
            return _context.Find<User>(id);
        }

        public void Save(User entity)
        {
            _context.Add(entity);
            _context.SaveChanges();
        }

        private void Validate(User user)
        {
            if (string.IsNullOrEmpty(user.Name))
                throw new ArgumentException("Name required");
        }
    }

    public record User(int Id, string Name, string Email);
}
