#include "leiden_interop.h"

#include <igraph/igraph.h>
#include <libleidenalg/GraphHelper.h>
#include <libleidenalg/ModularityVertexPartition.h>
#include <libleidenalg/CPMVertexPartition.h>
#include <libleidenalg/Optimiser.h>

#include <stdexcept>
#include <memory>
#include <vector>

extern "C" {

int leiden_find_communities(
    int n_nodes,
    int n_edges,
    const int* edge_from,
    const int* edge_to,
    int partition_type,
    double resolution,
    int seed,
    int* membership_out)
{
    if (n_nodes <= 0 || n_edges < 0 || !edge_from || !edge_to || !membership_out)
        return -1;

    try {
        // Build an igraph_vector_int_t of edges (pairs: from, to)
        igraph_vector_int_t edges;
        igraph_vector_int_init(&edges, n_edges * 2);
        for (int i = 0; i < n_edges; i++) {
            VECTOR(edges)[2 * i]     = (igraph_integer_t)edge_from[i];
            VECTOR(edges)[2 * i + 1] = (igraph_integer_t)edge_to[i];
        }

        igraph_t ig;
        igraph_create(&ig, &edges, (igraph_integer_t)n_nodes, IGRAPH_UNDIRECTED);
        igraph_vector_int_destroy(&edges);

        // Build edge weight vector (all 1.0)
        std::vector<double> edge_weights((size_t)n_edges, 1.0);

        // Wrap in libleidenalg Graph using static factory (edge weights only)
        Graph* graph = Graph::GraphFromEdgeWeights(&ig, edge_weights);

        // Create partition
        MutableVertexPartition* partition = nullptr;
        if (partition_type == 1) {
            partition = new CPMVertexPartition(graph, resolution);
        } else {
            partition = new ModularityVertexPartition(graph);
        }

        // Run Leiden optimiser
        Optimiser optimiser;
        if (seed >= 0) {
            optimiser.set_rng_seed((size_t)seed);
        }
        optimiser.optimise_partition(partition);

        // Copy membership to output array
        int n_communities = 0;
        for (int i = 0; i < n_nodes; i++) {
            int comm = (int)partition->membership((size_t)i);
            membership_out[i] = comm;
            if (comm + 1 > n_communities)
                n_communities = comm + 1;
        }

        delete partition;
        delete graph;
        igraph_destroy(&ig);

        return n_communities;

    } catch (const std::exception& e) {
        return -1;
    } catch (...) {
        return -1;
    }
}

} // extern "C"
