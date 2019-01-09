//-- CONSTANTS --\\
var radius = 5;
var defaultNodeCol = "white";
var highlightCol = "yellow";
var height = window.innerHeight;
var width = window.innerWidth;
var centerWidth = width / 2;
var centerHeight = height / 2;
var LinkThicknessBase = 1;
var LinkThicknessMult = 0.3;
var contextFont = "8pt Arial";
var contextFillStyle = "white";
var contextFillSelected = "gold";
var contextStokeStyle = "gray";

//-- Display Functions --\\
var nodeSwitch = 0;
var labelSwitch = 0;

function nodeOnOff(i){
    nodeSwitch = parseInt(d3.select("#nodesWhich").property("value"));
    simulationUpdate();
}
function textOnOff(){
    labelSwitch = parseInt(d3.select("#textWhich").property("value"));
    simulationUpdate();
}

function resetZoom(){
    // transform = transform.scale(1);
    // simulationUpdate();
}

function Recenter(){
    transform = d3.zoomIdentity;
    // transform.translate(0,0);
    simulationUpdate();
}

//-- d3 objects --\\
var links = [];
var nodes = [];
var selectedNode = null;

var transform = d3.zoomIdentity;

var linkForce = d3.forceLink()
                .id(function (link) { return link.id })
                .strength(function (link) { return link.strength });

var div = d3.select("body")
            .append("div")
            .attr("class", "tooltip")
            .style("opacity", 0);

var graphCanvas = d3.select('#graphDiv').append('canvas')
    .attr('width', width + 'px')
    .attr('height', height + 'px')
    .node();

var context = graphCanvas.getContext('2d', {alpha: false, imageSmoothingEnabled: true});

var simulation = d3.forceSimulation()
    .force("center", d3.forceCenter(centerWidth, centerHeight))
    .force("x", d3.forceX(width / 2).strength(0.1))
    .force("y", d3.forceY(height / 2).strength(0.1))
    .force("charge", d3.forceManyBody().strength(-120))
    .force("link", d3.forceLink().strength(1).id(function (d) { return d.id; }))
    .alphaTarget(0)
    .alphaDecay(0.05)


//-- d3 helpers --\\

/* Gets the track names on a given link */
function getLinkText(l) {
    return "・"+l.tracks.map((v, i, a) => v.name).join('<br/>・');
}

/* Gets thickness of link based on number of tracks */
function CalculateLinkThickness(tracks){
    var len = tracks.length;
    var ret = LinkThicknessBase + (len * LinkThicknessMult);
    return ret;
}

/* Returns a color for the node based on its proximity to our selected node. 2 == not connected */
function depth2color(depth){
    if(depth == 0){
        return "gold";
    }
    else if(depth == 1){
        return "green";
    }
    else if (depth == 2){
        return "red";
    }
    else if(depth == 3){
        return "#005895";
    }
    else if(depth == 4){
        return "#00139C";
    } 
    else if(depth == 5){
        return "#3700A3";
    } 
    else if(depth == 6){
        return "#8900AA";
    } else if (depth == 7){
        return "#B10080";
    } else if (depth == 8){
        return "#B8002F";
    }
    else {
        return "#5EBF00";
    }
}

/* Given a node, gets a list of nodes in its local graph */
function getNeighbors(node) {
    var fetched = [node.id];
    var toFetch = [node.id];
    var depth = 0;
    var arr = [{id: node.id, depth: depth}];
    node.depth = 0;
    while(toFetch.length !== 0){
        var nid = toFetch.pop();
        arr = links.reduce(function (neighbors, link) {
            var ltid = link.target.id;
            var lsid = link.source.id;
            if (ltid === nid && fetched.indexOf(lsid) === -1) {
                neighbors.push({id: lsid, depth: depth});
                nodes.find(x => x.id == ltid).depth = 1;
                fetched.push(lsid);
                toFetch.push(lsid);
            }
             else if (lsid === nid && fetched.indexOf(ltid) === -1) {
                neighbors.push({id: ltid, depth: depth});
                nodes.find(x => x.id == ltid).depth = 1;
                fetched.push(ltid);
                toFetch.push(ltid);
            }
            return neighbors;
        },
            arr
        );
    }
    return arr;
}

/* Determines whether a given (node,link) pair belong to each other */
function isNeighborLink(node, link) {
    return link.target.id === node.id || link.source.id === node.id;
}

/* Calculates distance between two objects. Must look like {x: num, y: num} */
function distance(A, B) {
    var t1 = A.x - B.x;
    var t2 = A.y - B.y;
    var t1s = t1*t1;
    var t2s = t2*t2;
    var ret = Math.sqrt(t1s+t2s);
    return ret;
}

/* Returns a link if the point is roughly near it, otherwise null */
function getHoveredLink(link, point){

    var AC = distance(link.source, point);
    var BC = distance(link.target, point);
    var AB = distance(link.source, link.target);

    var td = AC+BC;
    if(Math.abs(td - AB) <= 1){ // fuzzy matching
        return link;
    }
    return null;
}

/* Handles the zoom-pan of the simulation */
function zoomed() {
    transform = d3.event.transform;
    simulationUpdate();
}

/* Upon dragging the canvas, returns if a node is underneath */
function dragsubject() {
    var x = transform.invertX(d3.event.x);
    var y = transform.invertY(d3.event.y);

    for (var i = nodes.length - 1; i >= 0; --i) {
        var node = nodes[i];
        var dx = x - node.x;
        var dy = y - node.y;

        if (dx * dx + dy * dy < radius * radius) {
            node.x = transform.applyX(node.x);
            node.y = transform.applyY(node.y);
            return node;
        }
    }
}

/* Drags our node and updates our simulation
* we also use this to do our click event
*/
function dragstarted() {
    if (!d3.event.active){
        simulation.alphaTarget(0.7).restart();
    }
    d3.event.subject.fx = transform.invertX(d3.event.x);
    d3.event.subject.fy = transform.invertY(d3.event.y);
    
    //click hijack here
    selectNode(d3.event.subject);
}

/* while a drag is in motion, updates our drag subject */
function dragged() {
    d3.event.subject.fx = transform.invertX(d3.event.x);
    d3.event.subject.fy = transform.invertY(d3.event.y);
}

/* When the drag ends, run the simulation for a few more cycles */
function dragended() {
    if (!d3.event.active){
        simulation.alphaTarget(0);
    }
    d3.event.subject.fx = null;
    d3.event.subject.fy = null;
}

/* When a node is clicked, show the graph that it is connected to */
function selectNode(snode) {
    selectedNode = snode;

    // Find all neighbors
    var neighbors = getNeighbors(selectedNode);

    for(var i = 0; i < nodes.length; i++){
        nodes[i].depth = 2; //default
        for(var n = 0; n < neighbors.length; n++){
            if(neighbors[n].id === nodes[i].id){
                nodes[i].depth = 1;
            }
        }
    }
    selectedNode.depth = 0; 
}

/* The main tick function - redraws the canvas with our updated data */
function simulationUpdate() {
    context.save();

    context.clearRect(0, 0, width, height);
    context.translate(transform.x, transform.y);
    context.scale(transform.k, transform.k);

    // Draw the edges
    links.forEach(function (d) {
        context.beginPath();
        context.moveTo(d.source.x, d.source.y);
        context.lineTo(d.target.x, d.target.y);
        context.lineWidth =  CalculateLinkThickness(d.tracks);
        context.stroke();
        context.strokeStyle = contextStokeStyle;
    });

    // Draw the nodes
    nodes.forEach(function (d) {
        if( (nodeSwitch == 0) || (nodeSwitch == 1 && d.depth < 2) || (nodeSwitch == 2 && d.depth == 2)){
            context.beginPath();
            context.arc(d.x, d.y, radius, 0, 2 * Math.PI, true);
            context.fillStyle = depth2color(d.depth);
            context.fill();
        }
    });

    // Draw the text
    nodes.forEach(function (d) {
        if( (labelSwitch == 0) || (labelSwitch == 1 && d.depth < 2) || (labelSwitch == 2 && d.depth == 2)){
            context.font = contextFont;
            context.fillText(d.label, d.x+15, d.y+4);
            context.fillStyle = contextFillStyle;
        }
    });
    context.restore();
}

/* Checks is mouse is hovered over a link */
function mousemove(){
        
    var x = transform.invertX(d3.event.x);
    var y = transform.invertY(d3.event.y);

    for(var i = 0; i < links.length; i++){
        var link = links[i];
        res = getHoveredLink(link, {x: x, y: y});
        if(res){
            d3.select("#tooltip").style("opacity", 0.9).html(getLinkText(link)).style("left", d3.event.x+"px").style("top", (d3.event.y-28) +"px")
            break;
        }else{
            d3.select("#tooltip").style("opacity", 0.0);
        }
    }
}

//-- Main Function --\\
function mainf(data) {

    /* Assign main data */
    links = data.links;
    nodes = data.nodes;

    /* Reset all nodes to be in the red */
    nodes.forEach(x=>x.depth = 2);

    d3.select(graphCanvas)
        .on('mousemove', mousemove)
        .call(d3.drag().subject(dragsubject).on("start", dragstarted).on("drag", dragged).on("end", dragended))
        .call(d3.zoom().scaleExtent([1 / 10, 8]).on("zoom", zoomed))

    simulation.nodes(data.nodes)
        .on("tick", () => {
                //for (var i = 0; i < 3; i++) {
                    simulationUpdate();
                //}
            }
        );
    simulation.force("link")
        .links(data.links);
}


//-- KICKOFF --\\

// Download the data and kick off our graph
d3.json("/Graph/artistcollablink", function (error, data) {
    mainf(data);
});