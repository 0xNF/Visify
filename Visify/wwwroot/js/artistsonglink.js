"use strict";

// vars
var width = window.innerWidth;
var height = window.innerHeight;
var centerWidth = width/2;
var centerHeight = height/2;

var LinkThicknessBase = 1;
var LinkThicknessMult = 0.3;

var nodeOpacitySwitch = 0;
var textOpacitySwitch = 0;
var selectedNode = null;
var neighs = null;

let svg = null;
let g = null;
var nodeElements = null;

let curScale = 1.0;
let curX = 0;
let curY = 0;

let links = [];
let nodes = [];
let linkElements = null;
let textElements = null;
let div = null;
let linkForce = null;
let simulation = null;
var dragDrop = null;

let ns = [];

var print = console.log;

var zf2 =   
    d3.zoom().on("zoom", zoomfunc);

function zoomfunc() {
    print("ZFUNC zvar");
    print(d3.event.transform);
    curScale = d3.event.transform.k;
    curX = d3.event.transform.x;
    curY = d3.event.transform.y;
    g.attr("transform", d3.event.transform);
}

// DOM functions
function CalculateLinkThickness(tracks){
    return LinkThicknessBase;
}


function textOnOff(){
    textOpacitySwitch = parseInt($("#textWhich").val());
    if (textOpacitySwitch == 1){
        // none [all text is off]
        d3.selectAll("text").attr("opacity", 0);
    }else if(textOpacitySwitch == 2 || textOpacitySwitch == 3){
        if(selectNode !== null && neighs !== null && neighs.length > 0){
            textElements.attr('opacity', function(node){ return getTextOpacity(node, neighs)});
        } else{
            d3.selectAll("text").attr("opacity", 1);
        }
    } else{
        // default on
        d3.selectAll("text").attr("opacity", 1);
    }
}

function nodeOnOff(){
    nodeOpacitySwitch = parseInt($("#nodesWhich").val());
    if (nodeOpacitySwitch == 0){
        // all
        nodeElements.attr('opacity', 1);
        linkElements.attr('opacity', 1);
    } else{
        // every
        nodeElements.attr('opacity', function (node) { return getNodeOpacity(node, neighs) });
        linkElements.attr('opacity', function (link) { return getLinkOpacity(link, neighs) });
    }
}

function nodesOnOff() {

}

function ClearSearchText() {
    $("#searchText").val("");
}

function FindNode() {
    const t = $("#searchText").val();
    if (t.length == 0) {
        return;
    }
    let nx = 0;
    let ny = 0;
    let theNode = svg.selectAll('circle').filter(function (d) {
        if (d.label.toLocaleLowerCase().startsWith(t.toLocaleLowerCase())) {
            console.log(d.label);
            console.log(d);
            curX = d.x;
            curY = d.y;
            var zvar = d3.zoomIdentity.translate(d.y, d.x).scale(curScale);
            svg.transition().duration(750).call(zf2.transform, zvar);
            selectNode(d);
            return d;
        }
    });
}

// D3 Manip functions
function Recenter() {
    var zvar = d3.zoomIdentity.translate(centerWidth,centerHeight).scaleTo(curScale);
    svg.transition().duration(750).call(zf2.transform, zvar);
}

function ResetZoom() {
    curScale = 1.0;
    var zvar = d3.zoomIdentity;
    zvar.x = curX;
    zvar.y = curY;
    zvar.scale(1);
    print("RESET ZOOM zvar");
    print(zvar);
    print("END");
    svg.transition().duration(750).call(zf2.transform, zvar);
}

function resetThings() {
    g
        .attr("transform", "translate(" + curX + "," + curY + ")  scale(" + curScale + ")")
        .call(g.node().transform.scale, d3.zoomIdentity.scale(1));
}


function ResetMap() {
    Recenter();
    ResetZoom();
}

function selectNode(snode) {
    selectedNode = snode;
    var neighbors = getNeighbors(selectedNode);
    neighs = neighbors;

    // we modify the styles to highlight selected nodes
    nodeElements.attr('fill', function (node) { return getNodeColor(node, neighs) });
    nodeElements.attr('opacity', function (node) { return getNodeOpacity(node, neighs) });
    textElements.attr('fill', function (node) { return getTextColor(node, neighs) });
    textElements.attr('opacity', function(node){ return getTextOpacity(node, neighs)});
    linkElements.attr('stroke', function (link) { return getLinkColor(selectedNode, link) });
    linkElements.attr('opacity', function (link) { return getLinkOpacity(selectedNode, link) });

}

function getNeighbors(node) {
    var fetched = [node.id];
    var toFetch = [node.id];
    var depth = 0;
    var arr = [{id: node.id, depth: depth}];
    while(toFetch.length !== 0){
        var nid = toFetch.pop();
        depth+=1;
        arr = links.reduce(function (neighbors, link) {
            var ltid = link.target.id;
            var lsid = link.source.id;
            if (ltid === nid && fetched.indexOf(lsid) === -1) {
                neighbors.push({id: lsid, depth: depth});
                fetched.push(lsid);
                toFetch.push(lsid);
            }
             else if (lsid === nid && fetched.indexOf(ltid === -1)) {
                neighbors.push({id: ltid, depth: depth});
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

function isNeighborLink(node, link) {
    return link.target.id === node.id || link.source.id === node.id;
}

function depth2color(depth){
    if(depth == 0){
        return "green";
    }
    else if(depth == 1){
        return "#008740";
    }
    else if (depth == 2){
        return "#008E85";
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

function getNodeSize(node){
    var r = 10;
    var rmult = 3;
    if(node.level == 0){
        return r*rmult;
    } else{
        return r;
    }
}

function getTextSize(node){
    var sz = 15;
    var rmult = 3;
    if(node.level == 0){
        return sz*rmult;
    } else{
        return sz;
    }
}

function getNodeColor(node, neighbors) {
    var f = function(val){
        return val.id === node.id;
    }

    if(Array.isArray(neighbors)){
        var filtered = neighbors.filter(f);
        if(filtered.length >= 1){
            return depth2color(filtered[0].depth);
        }
    }

    return node.level === 1 ? 'red' : 'purple';
}

function getNodeOpacity(node, neighbors) {
    if(nodeOpacitySwitch == 0){
        return 1;
    }
    var f = function(val){
        return val.id === node.id; 
    }

    if(Array.isArray(neighbors)){
        var filtered = neighbors.filter(f);
        if(filtered.length >= 1 && nodeOpacitySwitch == 1){
            return 1;
        }
    }
    return 0;
}

function getLinkColor(node, link) {
    return isNeighborLink(node, link) ? 'green' : '#919191';
}

function getTextColor(node, neighbors) {
    return Array.isArray(neighbors) && neighbors.indexOf(node.id) > -1 ? 'green' : 'white';
}

function getTextOpacity(node, neighbors){

    if(textOpacitySwitch == 0){
        // indiscriminate all on
        return 1;
    } else if(textOpacitySwitch == 1){
        // indiscriminate all off
        return 0;
    }

    var f = function(val){
        return val.id === node.id;
    }

    if(Array.isArray(neighbors)){
        var filtered = neighbors.filter(f);
        if(filtered.length >= 1){
            if (textOpacitySwitch == 2) { 
                // only highlighted
                return 1;
            } else if(textOpacitySwitch == 3) {
                // non highlightd
                return 0;
            }
        }
    }
    return textOpacitySwitch === 2 ? 0 : textOpacitySwitch == 3 ? 1: 0;

}

function getLinkText(l) {
    // return l.tracks.map((v, i, a) => v.name).join('\n\n・\n\n');
}

function getNodeTextHover(n){
    if(n.level == 0){ //Artist
        return "👤  " + n.label + "("+ n.size +")";
    } else{
        return "🎵  " + n.label;
    }
}

function getLinkOpacity(link, neighbors) {
    if(nodeOpacitySwitch == 0){
        return 1;
    }
    var ltid = link.target.id;
    var lsid = link.source.id;

    var f = function(val){
        return (val.id == ltid || val.id == lsid);
    }
    if(neighbors.filter(f).length > 0){
        return 1;
    }
    return 0;
}


function mainf(j) {

    nodes = j["nodes"];
    links = j["links"];

    div = d3.select("body").append("div")
        .attr("class", "tooltip")
        .style("opacity", 0);

    svg = d3.select('svg')
        .attr("width", "100%")
        .attr("height", "100%")
        .call(zf2);
    g = svg.append("g");

    // simulation setup with all forces
    linkForce = d3
        .forceLink()
        .id(function (link) { return link.id })
        .strength(function (link) { return link.strength });

    simulation = d3
        .forceSimulation()
        .force('link', linkForce)
        .force('charge', d3.forceManyBody().strength(-120))
        .force('center', d3.forceCenter(centerWidth, centerHeight));

    dragDrop = d3.drag().on('start', function (node) {
        node.fx = node.x;
        node.fy = node.y;
    }).on('drag', function (node) {
        simulation.alphaTarget(0.7).restart();
        node.fx = d3.event.x;
        node.fy = d3.event.y;
    }).on('end', function (node) {
        if (!d3.event.active) {
            simulation.alphaTarget(0);
        }
        node.fx = null;
        node.fy = null;
    });


    linkElements = g.append("g")
        .attr("class", "links")
        .selectAll("line")
        .data(links)
        .enter().append("line")
        .attr("stroke-width", 1)
        .attr("stroke", "rgba(50, 50, 50, 0.2)");

    nodeElements =g.append("g")
        .attr("class", "nodes")
        .selectAll("circle")
        .data(nodes)
        .enter().append("circle")
        .attr("r", getNodeSize)
        .attr("fill", getNodeColor)
        .call(dragDrop)
        .on('click', selectNode);

    textElements = g.append("g")
        .attr("class", "texts")
        .selectAll("text")
        .data(nodes)
        .enter().append("text")
        .text(function (node) { return node.label })
        .attr("font-size", getTextSize)
        .attr("dx", 15)
        .attr("dy", 4);

    simulation.nodes(nodes).on('tick', () => {
        nodeElements
            .attr('cx', function (node) { return node.x })
            .attr('cy', function (node) { return node.y })
            .on("mouseover", function (node) {
                div.transition()
                    .duration(200)
                    .style("opacity", .9);
                div.text(getNodeTextHover(node))
                    .style("left", (d3.event.pageX) + "px")
                    .style("top", (d3.event.pageY - 28) + "px");
            })
            .on("mouseout", function (node) {
                div.transition()
                    .duration(500)
                    .style("opacity", 0);
            });
        textElements
            .attr('x', function (node) { return node.x })
            .attr('y', function (node) { return node.y })
        linkElements
            .attr('x1', function (link) { return link.source.x })
            .attr('y1', function (link) { return link.source.y })
            .attr('x2', function (link) { return link.target.x })
            .attr('y2', function (link) { return link.target.y })
            .attr("stroke-width", function(link){return CalculateLinkThickness(link.tracks)})
    });

    textOnOff();
    simulation.force("link").links(links);
}

$.getJSON("/Graph/artistsonglink", function (json) {
    mainf(json)
    console.log(json); // this will show the info it in firebug console
});


//$.getJSON("/json/nn_sct.json", function (json) {
//    mainf(json)
//    console.log(json); // this will show the info it in firebug console
//});