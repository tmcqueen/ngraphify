#pragma once
#ifdef __cplusplus
extern "C" {
#endif

/**
 * Find communities using the Leiden algorithm.
 *
 * @param n_nodes         Number of nodes in the graph
 * @param n_edges         Number of edges
 * @param edge_from       Array of source node indices (length n_edges)
 * @param edge_to         Array of target node indices (length n_edges)
 * @param partition_type  0 = Modularity, 1 = CPM
 * @param resolution      Resolution parameter (used for CPM; ignored for Modularity)
 * @param seed            Random seed (-1 for non-deterministic)
 * @param membership_out  Output array (must be pre-allocated, length n_nodes)
 *                        Filled with community assignments (0-indexed)
 * @return                Number of communities found, or -1 on error
 */
int leiden_find_communities(
    int n_nodes,
    int n_edges,
    const int* edge_from,
    const int* edge_to,
    int partition_type,
    double resolution,
    int seed,
    int* membership_out
);

#ifdef __cplusplus
}
#endif
