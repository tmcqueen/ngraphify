#include <iostream>
#include <vector>
#include "graph.h"

namespace algorithms {

class Graph {
public:
    Graph(int vertices);
    void addEdge(int u, int v);
    std::vector<int> bfs(int start);

private:
    int vertices_;
    std::vector<std::vector<int>> adj_;
};

Graph::Graph(int vertices) : vertices_(vertices), adj_(vertices) {}

void Graph::addEdge(int u, int v) {
    adj_[u].push_back(v);
    adj_[v].push_back(u);
}

std::vector<int> Graph::bfs(int start) {
    std::vector<int> result;
    // simplified
    return result;
}

template<typename T>
class Stack {
public:
    void push(T item);
    T pop();
    bool empty() const;
};

} // namespace algorithms

int main() {
    algorithms::Graph g(5);
    g.addEdge(0, 1);
    g.addEdge(1, 2);
    auto result = g.bfs(0);
    std::cout << result.size() << std::endl;
    return 0;
}
