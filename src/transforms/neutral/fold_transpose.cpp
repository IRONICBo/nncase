#include <ir/ops/transpose.h>
#include <ir/visitor.h>
#include <transforms/neutral/fold_transpose.h>

using namespace nncase;
using namespace nncase::ir;
using namespace nncase::transforms;

// Transpose (perm = p1)
//     |
//     v
// Transpose (perm = p2)
//
// y1[i] = x1[p1[i]]
// y2[i] = x2[p2[i]]
// x2 = y1
// =>
// y2[i] = y1[p2[i]] = x1[p1[p2[i]]]

bool fold_transpose_transform::on_try_match(node &node, transform_context &context)
{
    if (node.runtime_opcode() == op_transpose)
    {
        auto &tp1 = static_cast<transpose &>(node);
        if (auto tp2 = try_get_direct_child<transpose>(tp1))
        {
            if (tp1.perm().size() == tp2->perm().size())
            {
                context.inputs.emplace_back(&tp1.input());
                context.outputs.emplace_back(&tp2->output());

                context.matched_nodes.emplace_back(&tp1);
                context.matched_nodes.emplace_back(tp2);
                return true;
            }
        }
    }

    return false;
}

void fold_transpose_transform::process(transform_context &context)
{
    auto &output = *context.inputs[0]->connection();
    auto inputs = context.outputs[0]->connections();

    auto &p1 = static_cast<transpose *>(context.matched_nodes[0])->perm();
    auto &p2 = static_cast<transpose *>(context.matched_nodes[1])->perm();

    axis_t perm(p1.size());
    for (size_t i = 0; i < p1.size(); i++)
        perm[i] = p1[p2[i]];

    auto tp = context.graph.emplace<transpose>(output.type(), output.shape(), perm);
    tp->input().connect(output);

    for (auto &in : dup(inputs))
        in->connect(tp->output());
}

// Transpose (perm = p1)
//
// p1[i] = i

bool fold_nop_transpose_transform::on_try_match(node &node, transform_context &context)
{
    if (node.runtime_opcode() == op_transpose)
    {
        auto &tp = static_cast<transpose &>(node);

        for (size_t i = 0; i < tp.perm().size(); i++)
        {
            if (tp.perm()[i] != i)
                return false;
        }

        context.inputs.emplace_back(&tp.input());
        context.outputs.emplace_back(&tp.output());

        context.matched_nodes.emplace_back(&tp);
        return true;
    }

    return false;
}

void fold_nop_transpose_transform::process(transform_context &context)
{
    auto &output = *context.inputs[0]->connection();
    auto inputs = context.outputs[0]->connections();

    for (auto &in : dup(inputs))
        in->connect(output);
}
