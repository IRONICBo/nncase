#pragma once 
#include "node.h"

namespace nncase
{
namespace ir
{
    class output_node : public node
    {
    public:
        DEFINE_NODE_OPCODE(op_output_node);

        input_connector &input() { return input_at(0); }

        template <class TShape>
        output_node(datatype_t type, TShape &&shape)
        {
            add_input("input", type, std::forward<TShape>(shape));
        }
    };
}
}
