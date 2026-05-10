package com.example.service;

import java.util.List;
import java.util.ArrayList;
import com.example.model.User;

public class UserService {
    private final UserRepository repository;

    public UserService(UserRepository repository) {
        this.repository = repository;
    }

    public User findUser(int id) {
        return repository.findById(id);
    }

    public List<User> listAll() {
        return new ArrayList<>(repository.findAll());
    }

    private void validate(User user) {
        if (user.getName() == null) {
            throw new IllegalArgumentException("Name required");
        }
    }
}

interface UserRepository {
    User findById(int id);
    List<User> findAll();
}
